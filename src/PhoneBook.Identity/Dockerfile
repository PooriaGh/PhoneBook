# Build context: repository root.   docker build -f src/PhoneBook.Identity/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first for layer caching. global.json is deliberately not copied: it pins the local SDK band.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/PhoneBook.Identity/PhoneBook.Identity.csproj src/PhoneBook.Identity/
RUN dotnet restore src/PhoneBook.Identity/PhoneBook.Identity.csproj

COPY src/PhoneBook.Identity/ src/PhoneBook.Identity/
RUN dotnet publish src/PhoneBook.Identity/PhoneBook.Identity.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "PhoneBook.Identity.dll"]
