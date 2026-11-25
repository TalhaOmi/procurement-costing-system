FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Restore first so the package layer is cached until a .csproj changes.
COPY src/Erp.Core/Erp.Core.csproj src/Erp.Core/
COPY src/Erp.Api/Erp.Api.csproj src/Erp.Api/
RUN dotnet restore src/Erp.Api/Erp.Api.csproj
COPY src/ src/
RUN dotnet publish src/Erp.Api/Erp.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /data
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Erp="Data Source=/data/erp.db" \
    Erp__SeedDemoData=true
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "Erp.Api.dll"]
