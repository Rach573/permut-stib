# Permut STIB

Application privée d'entraide pour la gestion des permutations de vacances et des signatures entre agents.

## Architecture

Le projet impose une séparation stricte des responsabilités :

1. `frontend/` — Vue 3 + TypeScript. Le navigateur appelle uniquement l'API HTTP.
2. `backend/PermutStib.Api` + `backend/PermutStib.Business` — endpoints, authentification et règles métier.
3. `backend/PermutStib.Data` — Entity Framework Core, ASP.NET Core Identity et PostgreSQL.

Le frontend ne contient aucune chaîne de connexion, aucun ORM et aucun accès direct à PostgreSQL.

## Authentification V1

- inscription avec matricule STIB, GSM et mot de passe ;
- compte créé avec le statut `Pending` ;
- validation ou refus par un délégué ;
- connexion autorisée uniquement pour un compte `Active` ;
- mot de passe haché côté serveur ;
- authentification par cookie `HttpOnly`.

## Développement local

Prérequis : .NET 10 SDK, Node.js, Docker.

```bash
docker compose up -d postgres
dotnet restore backend/PermutStib.Api/PermutStib.Api.csproj
dotnet run --project backend/PermutStib.Api
cd frontend
npm install
npm run dev
```

La V1 est volontairement un socle : le modèle d'authentification et la séparation 3 couches sont posés avant l'implémentation complète des workflows de permutation et de signature.

