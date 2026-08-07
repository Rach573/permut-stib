# Architecture 3 couches

## Règle de dépendance

```text
Navigateur / Vue
      |
      | HTTPS / JSON uniquement
      v
PermutStib.Api
      |
      v
PermutStib.Business
      ^
      |
PermutStib.Data  ---> PostgreSQL
```

`PermutStib.Api` référence `PermutStib.Data` uniquement dans le composition root (`Program.cs`) pour enregistrer les implémentations dans l'injection de dépendances. Les contrôleurs dépendent des services métier, pas du `DbContext` ni des repositories.

## Couche présentation

- `frontend/` : Vue 3 + TypeScript.
- `PermutStib.Api/Controllers` : frontière HTTP du backend.
- Aucun package PostgreSQL, EF Core ou chaîne de connexion dans `frontend/`.

## Couche métier

- `PermutStib.Business` contient les modèles métier, validations, services et interfaces nécessaires.
- Cette couche ne référence ni ASP.NET Core, ni Entity Framework Core, ni PostgreSQL.

## Couche données

- `PermutStib.Data` implémente les interfaces définies par la couche métier.
- Elle seule connaît Entity Framework Core, ASP.NET Core Identity et Npgsql/PostgreSQL.
- Les credentials PostgreSQL restent côté serveur.

## Authentification

- Le frontend envoie matricule/GSM + mot de passe à `/api/auth/login`.
- Le backend vérifie le mot de passe avec ASP.NET Core Identity.
- Le navigateur reçoit uniquement un cookie de session `HttpOnly` et les données publiques utiles du compte.
- `PasswordHash`, chaînes de connexion et objets EF ne sont jamais sérialisés vers le frontend.

## Initialisation du premier délégué

Le premier compte administrateur est créé au démarrage uniquement si les variables de configuration suivantes sont fournies au backend :

- `BootstrapAdmin__Matricule`
- `BootstrapAdmin__PhoneNumber`
- `BootstrapAdmin__Password`

Le mot de passe ne doit jamais être commité dans Git.

