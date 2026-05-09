# Licensing Challenge

Plateforme SaaS de gestion de licences et de quotas, construite en microservices avec **.NET 9**.

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                     Client (HTTP)                       │
└─────────────────────┬───────────────────────────────────┘
                      │ :8080
          ┌───────────▼───────────┐
          │     Gateway (YARP)    │   Reverse proxy
          └──────┬────────┬───────┘
                 │        │
         :8081   │        │ :8082
    ┌────▼─────┐ │  ┌─────▼──────┐
    │ Licensing │ │  │  Metering  │
    │  Service  │ │  │  Service   │
    └────┬──────┘ │  └─────┬──────┘
         │        │        │
    ┌────▼──────┐ │  ┌─────▼──────┐
    │ PostgreSQL │ │  │   Redis    │
    └───────────┘ │  └────────────┘
                  │
         (Docker Compose)
```

| Service            | Port | Rôle                                      |
|--------------------|------|-------------------------------------------|
| `gateway`          | 8080 | Point d'entrée unique, routing YARP        |
| `licensing-service`| 8081 | Licences, apps, jobs — persistance PG      |
| `metering-service` | 8082 | Quotas 24 h — compteur Redis atomique      |
| `postgres`         | 5432 | Base relationnelle                         |
| `redis`            | 6379 | Cache distribué / sliding window           |

---

## Stack technique

| Couche            | Technologie                                    |
|-------------------|------------------------------------------------|
| Runtime           | .NET 9 / ASP.NET Core                         |
| Proxy             | YARP 2.1                                       |
| ORM               | Entity Framework Core 9 + Npgsql              |
| Cache / Quota     | StackExchange.Redis 2.7 — Sorted Sets + Lua   |
| Auth              | JWT Bearer — HMAC-SHA256                       |
| Tests             | xUnit, Moq, FluentAssertions                   |
| Conteneurs        | Docker multi-stage, Docker Compose 3.9         |

---

## Services

### Gateway

Reverse proxy YARP sans logique métier. Il route les requêtes entrantes vers les services backend selon le préfixe de chemin :

```
/api/licenses/*  →  licensing-service:8081
/api/apps/*      →  licensing-service:8081
/api/jobs/*      →  licensing-service:8081
/api/metering/*  →  metering-service:8082
```

---

### Licensing Service

Gère le cycle de vie des licences multi-tenant, l'enregistrement des applications et le suivi des jobs.

#### Modèles de données

```
License
├── Id (Guid, PK)
├── TenantId (string, unique index)
├── MaxApps (int)
├── MaxExecutionsPer24h (int)
├── ValidFrom / ValidTo (DateTime)
├── Status (ACTIVE | SUSPENDED | EXPIRED | REVOKED)
└── Apps []
    ├── Id, Name, Description
    ├── LicenseId (FK → License, cascade delete)
    └── Jobs []
        ├── Id, Name
        ├── AppId (FK → App, cascade delete)
        ├── Status (RUNNING | FINISHED)
        └── StartedAt / FinishedAt
```

#### Endpoints

**Licences** — `POST /api/licenses`

| Méthode | Chemin                  | Auth   | Description                        |
|---------|-------------------------|--------|------------------------------------|
| POST    | `/api/licenses`         | —      | Créer une licence → retourne JWT   |
| GET     | `/api/licenses`         | —      | Lister toutes les licences         |
| GET     | `/api/licenses/{tenantId}` | —   | Récupérer par tenant               |
| POST    | `/api/licenses/validate`| —      | Valider un JWT                     |
| POST    | `/api/licenses/revoke`  | —      | Révoquer une licence               |
| POST    | `/api/licenses/upgrade` | —      | Modifier quotas ou validité        |

**Applications** — Bearer token requis

| Méthode | Chemin           | Description                             |
|---------|------------------|-----------------------------------------|
| POST    | `/api/apps/register` | Enregistrer une app (quota enforced) |
| GET     | `/api/apps`      | Lister les apps du tenant connecté      |

**Jobs** — Bearer token requis

| Méthode | Chemin           | Description                           |
|---------|------------------|---------------------------------------|
| POST    | `/api/jobs/start`  | Démarrer un job (status = RUNNING)  |
| POST    | `/api/jobs/finish` | Terminer un job (status = FINISHED) |
| GET     | `/api/jobs`        | Lister les jobs du tenant connecté  |

#### Authentification JWT

À la création d'une licence, un JWT signé HMAC-SHA256 est retourné. Il contient les claims :

```json
{
  "tenantId": "...",
  "licenseId": "...",
  "maxApps": 10,
  "maxExecutionsPer24h": 1000,
  "status": "ACTIVE"
}
```

Ce token est ensuite utilisé comme Bearer token pour les endpoints apps et jobs.

---

### Metering Service

Applique les quotas d'exécution en temps réel via un **sliding window de 24 h** implémenté avec des Sorted Sets Redis et un script Lua atomique.

#### Algorithme (Lua, atomique)

```
1. ZREMRANGEBYSCORE  → supprimer les entrées > 24 h
2. ZCARD             → compter les exécutions restantes
3. Si count < max    → ZADD (timestamp, uuid) + EXPIRE 86400
4. Retourner (allowed, used, remaining)
```

Structure Redis : `metering:{tenantId}:executions`
- **Score** : timestamp Unix (secondes)
- **Member** : UUID unique par exécution

#### Endpoints

| Méthode | Chemin                       | Description                           |
|---------|------------------------------|---------------------------------------|
| POST    | `/api/metering/check`        | Check + incrément atomique (429 si dépassé) |
| GET     | `/api/metering/{tenantId}`   | Statut courant du quota               |
| POST    | `/api/metering/{tenantId}/decrement` | Annuler une exécution         |
| DELETE  | `/api/metering/{tenantId}`   | Reset du compteur                     |

---

## Flux d'utilisation typique

```
1. POST /api/licenses
   → { licenseId, token, ... }

2. POST /api/apps/register          [Authorization: Bearer <token>]
   → { appId, name, ... }

3. POST /api/jobs/start             [Authorization: Bearer <token>]
   Body: { jobName, appId }
   → { jobId, status: "RUNNING" }

4. POST /api/metering/check
   Body: { tenantId, maxExecutionsPer24h }
   → 200 OK  { allowed: true, used, remaining }
   → 429     { allowed: false, ... }

5. POST /api/jobs/finish            [Authorization: Bearer <token>]
   Body: { jobId }
   → { jobId, status: "FINISHED", finishedAt }
```

---

## Lancer le projet

### Prérequis

- Docker Desktop

### Démarrage

```bash
docker compose up --build -d
```

Les services démarrent dans l'ordre : `postgres` et `redis` (health checks) → `licensing-service` et `metering-service` → `gateway`.

Les migrations EF Core sont appliquées automatiquement au démarrage du licensing-service.

### Points d'accès

| URL                          | Description               |
|------------------------------|---------------------------|
| `http://localhost:8080`      | Gateway (point d'entrée)  |
| `http://localhost:8080/health` | Health check             |
| `http://localhost:8081`      | Licensing Service (direct)|
| `http://localhost:8082`      | Metering Service (direct) |

---

## Tests

```bash
dotnet test
```

Les tests couvrent :

| Suite                    | Type        | Description                                      |
|--------------------------|-------------|--------------------------------------------------|
| `TokenServiceTests`      | Unitaire    | Génération JWT, validation, détection de tampering |
| `LicenseServiceTests`    | Unitaire    | Cycle de vie, doublons, upgrade, révocation       |
| `AppServiceTests`        | Unitaire    | Enregistrement, enforcement du quota d'apps       |
| `JobServiceTests`        | Unitaire    | Start/finish, double-finish, ownership            |
| `RepositoryTests`        | Intégration | EF Core in-memory, cascade deletes                |
| `MeteringServiceTests`   | Unitaire    | Sliding window, isolation tenant, decrement       |

Les services sont testés via des mocks de repositories. Le metering utilise une implémentation in-memory (`InMemoryMeteringService`) qui reproduit le comportement du Lua Redis.

---

## Variables d'environnement

| Variable                              | Service           | Description                  |
|---------------------------------------|-------------------|------------------------------|
| `ConnectionStrings__DefaultConnection`| licensing-service | Chaîne PostgreSQL             |
| `Jwt__Secret`                         | licensing-service | Clé HMAC-SHA256 (≥ 256 bits) |
| `Redis__ConnectionString`             | metering-service  | Adresse Redis (`host:port`)  |
| `ASPNETCORE_ENVIRONMENT`              | tous              | `Development` / `Production` |

---

## Structure du projet

```
licensing-challenge/
├── docker-compose.yml
├── LicensingChallenge.sln
├── gateway/
│   ├── Dockerfile
│   └── src/
│       ├── Program.cs
│       └── appsettings.json          # Routes YARP
├── licensing-service/
│   ├── Dockerfile
│   └── src/
│       ├── Program.cs                # DI setup + migrations
│       ├── Data/LicensingDbContext.cs
│       ├── Models/                   # License, App, Job
│       ├── DTOs/                     # Requests & Responses
│       ├── Repositories/
│       ├── Services/
│       └── Controllers/Controllers.cs
├── metering-service/
│   ├── Dockerfile
│   └── src/
│       ├── Program.cs
│       ├── Services/Implementations/RedisMeteringService.cs
│       └── Controllers/MeteringController.cs
└── tests/
    └── unit/src/
        ├── TokenServiceTests.cs
        ├── LicenseServiceTests.cs
        ├── AppServiceTests.cs
        ├── JobServiceTests.cs
        ├── RepositoryTests.cs
        └── MeteringServiceTests.cs
```
