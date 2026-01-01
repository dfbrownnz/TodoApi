# Stage 1: Build the application using the .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file and restore dependencies (for faster caching)
COPY ["TodoApi.csproj", "./"]
RUN dotnet restore "./TodoApi.csproj"

# Copy the remaining source code and build the app
COPY . .
RUN dotnet publish "TodoApi.csproj" -c Release -o /app/publish

# Stage 2: Create the final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Cloud Run uses port 8080 by default for .NET 8+
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Copy the published output from the build stage
COPY --from=build /app/publish .

# Start the application
ENTRYPOINT ["dotnet", "TodoApi.dll"]