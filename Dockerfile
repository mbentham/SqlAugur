FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SqlAugur.slnx .
COPY SqlAugur/SqlAugur.csproj SqlAugur/
RUN dotnet restore SqlAugur/SqlAugur.csproj
COPY SqlAugur/ SqlAugur/
RUN dotnet publish SqlAugur/SqlAugur.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SqlAugur.dll"]
