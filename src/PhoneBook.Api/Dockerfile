# Build context: repository root.   docker build -f src/PhoneBook.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first for layer caching. global.json is deliberately not copied: it pins the local SDK band.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/PhoneBook.SharedKernel/PhoneBook.SharedKernel.csproj src/PhoneBook.SharedKernel/
COPY src/PhoneBook.Domain/PhoneBook.Domain.csproj src/PhoneBook.Domain/
COPY src/PhoneBook.Application/PhoneBook.Application.csproj src/PhoneBook.Application/
COPY src/PhoneBook.Infrastructure/PhoneBook.Infrastructure.csproj src/PhoneBook.Infrastructure/
COPY src/PhoneBook.Api/PhoneBook.Api.csproj src/PhoneBook.Api/
RUN dotnet restore src/PhoneBook.Api/PhoneBook.Api.csproj

COPY src/ src/
RUN dotnet publish src/PhoneBook.Api/PhoneBook.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "PhoneBook.Api.dll"]
