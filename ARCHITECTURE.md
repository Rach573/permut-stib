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

## Correspondance avec SnowDispatcher

| SnowDispatcher | Permut STIB | Responsabilité |
|---|---|---|
| `Api` | `PermutStib.Api` | HTTP, cookies, autorisations, sérialisation |
| `Core` | `PermutStib.Business` | modèles, règles, cas d'usage, interfaces de gateways |
| `Infrastructure` | `PermutStib.Data` | Identity, EF Core, PostgreSQL, audit et notifications persistées |

La différence de nom ne change pas la règle de dépendance. `Business` ne référence aucun projet. `Data` référence uniquement `Business`. `Api` référence les deux uniquement pour composer l'application.

Les transitions de permutation, de signature et d'administration sont définies dans `PermutStib.Business/Rules`. La couche Data charge l'état persistant, applique ces règles métier et sauvegarde le résultat dans une transaction lorsque plusieurs écritures doivent rester atomiques.

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
- le GSM n'est renvoyé que par les endpoints protégés par la politique `AdminOnly` ;
- une suspension invalide immédiatement une session déjà ouverte ;
- les tentatives de connexion sont limitées et les écritures exigent un en-tête applicatif non envoyable par un formulaire tiers ;
- en Production, le cookie est obligatoirement `Secure` et les secrets proviennent uniquement des variables d'environnement.

## Contrôle automatique

`scripts/verify-alpha.ps1` vérifie les parcours complets avec plusieurs sessions indépendantes : inscription, doublon GSM, activation, rôles, confidentialité, permutations concurrentes, double confirmation, signatures, notifications, audit, suspension et déconnexion.

## Initialisation du premier délégué

Le premier compte administrateur est créé au démarrage uniquement si les variables de configuration suivantes sont fournies au backend :

- `BootstrapAdmin__Matricule`
- `BootstrapAdmin__PhoneNumber`
- `BootstrapAdmin__Password`

Le mot de passe ne doit jamais être commité dans Git.
