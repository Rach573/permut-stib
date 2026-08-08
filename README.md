# Permut STIB

Application privée d'entraide pour la gestion des permutations de vacances et des signatures entre agents.

## Architecture

Le projet impose une séparation stricte des responsabilités :

1. `frontend/` — Vue 3 + TypeScript. Le navigateur appelle uniquement l'API HTTP.
2. `backend/PermutStib.Api` — endpoints, cookies et autorisations HTTP.
3. `backend/PermutStib.Business` — modèles, cas d'usage, règles métier pures et interfaces de gateways.
4. `backend/PermutStib.Data` — Entity Framework Core, ASP.NET Core Identity, transactions et PostgreSQL.

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

### Comptes de démonstration

En environnement `Development`, une base neuve reçoit automatiquement 50 agents fictifs et un compte délégué.

- délégué : `DELEGUE`
- agent actif : `70-001` à `70-042`
- mot de passe commun : `test1234`

Les comptes `70-043` à `70-046` sont en attente, `70-047` et `70-048` sont suspendus, et les deux derniers sont refusés.

## Vérification alpha

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-alpha.ps1
```

Le script exécute les parcours complets avec plusieurs agents et échoue dès qu'une réponse HTTP, une règle métier, une notification, un droit ou la confidentialité du GSM n'est pas respecté.

## Déploiement de démonstration

Le dépôt contient un `Dockerfile` multi-stage et un Blueprint `render.yaml`. La combinaison recommandée est :

- Render Free pour l'application Docker et le certificat HTTPS ;
- Neon Free pour PostgreSQL persistant sans expiration automatique à 30 jours ;
- la chaîne PostgreSQL Neon dans `ConnectionStrings__Postgres` ;
- les secrets du délégué dans `BootstrapAdmin__PhoneNumber` et `BootstrapAdmin__Password`.

La configuration de développement et son mot de passe local ne sont jamais copiés dans le paquet publié.
