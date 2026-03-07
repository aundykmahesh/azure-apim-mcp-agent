FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/AzureApimMcp.Functions/AzureApimMcp.Functions.csproj src/AzureApimMcp.Functions/
RUN dotnet restore src/AzureApimMcp.Functions/AzureApimMcp.Functions.csproj

COPY . .
WORKDIR /src/src/AzureApimMcp.Functions
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
COPY --from=build /app/publish /home/site/wwwroot
