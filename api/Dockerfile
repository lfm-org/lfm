FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/Lfm.Contracts/Lfm.Contracts.csproj shared/Lfm.Contracts/
COPY api/Lfm.Api.csproj api/
RUN dotnet restore api/Lfm.Api.csproj

COPY shared/ shared/
COPY api/ api/
RUN dotnet publish api/Lfm.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
COPY --from=build /app/publish /home/site/wwwroot
