# FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
  
# WORKDIR /app
# COPY . ./

# RUN sed -i 's#<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="5.0.0" />#<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="5.0.0" />#' src/SimplCommerce.WebHost/SimplCommerce.WebHost.csproj
# RUN sed -i 's/UseSqlServer/UseNpgsql/' src/SimplCommerce.WebHost/Program.cs
# RUN sed -i 's/UseSqlServer/UseNpgsql/' src/SimplCommerce.WebHost/Extensions/ServiceCollectionExtensions.cs

# RUN rm src/SimplCommerce.WebHost/Migrations/* && cp -f src/SimplCommerce.WebHost/appsettings.docker.json src/SimplCommerce.WebHost/appsettings.json

# RUN dotnet tool install --global dotnet-ef --version 5.0.0
# ENV PATH="${PATH}:/root/.dotnet/tools"

# # ef core migrations run in debug, so we have to build in Debug for copying module correctly 
# RUN dotnet restore && dotnet build \
#     && cd src/SimplCommerce.WebHost \
# 	&& dotnet ef migrations add initialSchema \
#     && dotnet ef migrations script -o dbscript.sql

# RUN dotnet build -c Release \
# 	&& cd src/SimplCommerce.WebHost \
#     && dotnet build -c Release \
# 	&& dotnet publish -c Release -o out
	
# RUN curl -SL "https://github.com/rdvojmoc/DinkToPdf/raw/v1.0.8/v0.12.4/64%20bit/libwkhtmltox.so" --output /app/src/SimplCommerce.WebHost/out/libwkhtmltox.so

# # remove BOM for psql	
# RUN sed -i -e '1s/^\xEF\xBB\xBF//' /app/src/SimplCommerce.WebHost/dbscript.sql

# FROM mcr.microsoft.com/dotnet/aspnet:5.0

# # hack to make postgresql-client install work on slim
# RUN mkdir -p /usr/share/man/man1 \
#     && mkdir -p /usr/share/man/man7

# RUN apt-get update \
# 	&& apt-get install -y --no-install-recommends postgresql-client \
# 	&& apt-get install libgdiplus -y \
# 	&& rm -rf /var/lib/apt/lists/*

# WORKDIR /app	
# COPY --from=build-env /app/src/SimplCommerce.WebHost/out ./
# COPY --from=build-env /app/src/SimplCommerce.WebHost/dbscript.sql ./

# COPY --from=build-env /app/docker-entrypoint.sh /
# RUN chmod 755 /docker-entrypoint.sh

# ENTRYPOINT ["/docker-entrypoint.sh"]
# 1. Use the .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app
COPY . ./

# Remove conflicting global.json if it exists
RUN rm -f global.json

# Update project to use PostgreSQL instead of SQL Server
RUN sed -i 's#<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />#<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />#' src/SimplCommerce.WebHost/SimplCommerce.WebHost.csproj
RUN sed -i 's/UseSqlServer/UseNpgsql/' src/SimplCommerce.WebHost/Program.cs
RUN sed -i 's/UseSqlServer/UseNpgsql/' src/SimplCommerce.WebHost/Extensions/ServiceCollectionExtensions.cs

# Setup appsettings for Docker
RUN rm src/SimplCommerce.WebHost/Migrations/* && cp -f src/SimplCommerce.WebHost/appsettings.docker.json src/SimplCommerce.WebHost/appsettings.json

# Install EF Core tools (Version 8.0.0+)
RUN dotnet tool install --global dotnet-ef --version 8.0.0
ENV PATH="${PATH}:/root/.dotnet/tools"

# Build and generate migration script
RUN dotnet restore && dotnet build \
    && cd src/SimplCommerce.WebHost \
    && dotnet ef migrations add initialSchema \
    && dotnet ef migrations script -o dbscript.sql

# Publish the application
RUN dotnet publish src/SimplCommerce.WebHost/SimplCommerce.WebHost.csproj -c Release -o out

# 2. Use the .NET 8 Runtime for the final image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Standard package install for Debian 12 (Bookworm)
RUN apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client libgdiplus \
    && rm -rf /var/lib/apt/lists/*

# Copy output from build stage
COPY --from=build-env /app/src/SimplCommerce.WebHost/out ./
COPY --from=build-env /app/src/SimplCommerce.WebHost/dbscript.sql ./

# Ensure entrypoint script is accessible
COPY --from=build-env /app/docker-entrypoint.sh /
RUN chmod 755 /docker-entrypoint.sh

# Render uses port 10000 by default
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["/docker-entrypoint.sh"]