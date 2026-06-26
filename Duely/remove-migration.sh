#!/bin/bash

dotnet ef migrations remove --startup-project src/Duely --project src/Duely.Infrastructure.DataAccess.EntityFramework
