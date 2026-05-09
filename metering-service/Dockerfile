FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY src/*.csproj ./
RUN dotnet restore
COPY src/ ./
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /out .
EXPOSE 8082
ENV ASPNETCORE_URLS=http://+:8082
ENTRYPOINT ["dotnet", "MeteringService.dll"]
