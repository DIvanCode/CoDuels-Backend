#!/bin/bash

if [[ $# -ne 1 ]]; then
  dotnet ef database update --startup-project src/Duely --project src/Duely.Infrastructure.DataAccess.EntityFramework
else
  dotnet ef database update $1 --startup-project src/Duely --project src/Duely.Infrastructure.DataAccess.EntityFramework
fi

