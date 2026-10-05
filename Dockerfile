# Builds the app into a small image for hosting. Two stages: the SDK image compiles and
# publishes, and only the published output goes into the runtime image.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore first, on its own layer, so a code change does not re-download packages.
COPY src/CraftingPlanner.Api/CraftingPlanner.Api.csproj src/CraftingPlanner.Api/
RUN dotnet restore src/CraftingPlanner.Api/CraftingPlanner.Api.csproj

COPY src/ src/
RUN dotnet publish src/CraftingPlanner.Api/CraftingPlanner.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# The host tells the app which port to listen on through this variable; 10000 is what
# Render expects by default.
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "CraftingPlanner.Api.dll"]
