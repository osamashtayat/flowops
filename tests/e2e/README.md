# E2E smoke (QA-04)

Playwright against the OPS-01 Docker Compose stack, seeded with the BE-16 demo company. Two flows:

- **purchase.spec.ts**: Lina submits a purchase → Samir (manager) approves → Farah (finance) approves → Yousef (procurement) fulfils → Lina sees it completed.
- **leave.spec.ts**: Karim asks for leave → Samir rejects with a reason (the empty reason is refused first) → Karim sees the rejection and the reason.

They sign in with the demo accounts documented in the repository README ("Demo data"); no other credentials are used.

## Run locally

```bash
cd tests/e2e
npm ci
npx playwright install chromium
./stack.sh up        # fresh, isolated stack: project flowops-e2e, https://localhost:9443, db on 127.0.0.1:55432
npx playwright test
./stack.sh down      # removes its containers and volumes
```

`./stack.sh up` always starts from an empty database, and it never touches the dev stack (`flowops` project, port 8443/5432).
Override ports with `E2E_WEB_PORT` / `E2E_DB_PORT` and the URL with `E2E_BASE_URL`. `./stack.sh logs` prints the api and web logs after a failure; `npx playwright show-trace test-results/<test>/trace.zip` replays it.

Titles carry a per-run suffix, so the specs can run repeatedly against the same stack.
