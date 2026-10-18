FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Blockchain.Domain/Blockchain.Domain.csproj src/Blockchain.Domain/
COPY src/Blockchain.Application/Blockchain.Application.csproj src/Blockchain.Application/
COPY src/Blockchain.Infrastructure/Blockchain.Infrastructure.csproj src/Blockchain.Infrastructure/
COPY src/Blockchain.Api/Blockchain.Api.csproj src/Blockchain.Api/
RUN dotnet restore src/Blockchain.Api/Blockchain.Api.csproj
COPY src/ src/
RUN dotnet publish src/Blockchain.Api/Blockchain.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Blockchain.Api.dll"]
