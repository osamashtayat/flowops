# contracts/

`openapi.json` here is **generated** from the API (task BE-03) and committed. It is the executable form of `docs/API_CONTRACT.md`.

- Regenerate: `./scripts/export-openapi.sh` (created in BE-03).
- Frontend types: `cd src/frontend && npm run api:types` (added in FE-01).
- CI fails if the committed file differs from what the API produces. Never edit it by hand.
