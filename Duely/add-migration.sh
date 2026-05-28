#!/bin/bash

if [[ $# -ne 1 ]]; then
    echo "Error: Expected 1 arguments, got $#." >&2
    echo "Usage: $0 <migration_name>" >&2
    exit 1
fi

if [[ -z "$1" ]]; then
    echo "Error: <migration_name> cannot be empty." >&2
    exit 1
fi

dotnet ef migrations add $1 --startup-project src/Duely --project src/Duely.Infrastructure.DataAccess.EntityFramework