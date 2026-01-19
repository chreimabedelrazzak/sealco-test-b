#!/bin/sh
set -e

cd /app
exec dotnet SimplCommerce.WebHost.dll
