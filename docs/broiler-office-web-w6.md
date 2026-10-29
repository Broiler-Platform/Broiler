# Broiler Office web — W6 delivery and operations

**Hosting update:** the user's subsequent website configuration hosts all three
apps at `broiler-platform.github.io/demo/`, `/writer/` and `/cell/`, with HTTP
forwarding from the short domains. The website workflow resolves current
published producer assets and deploys the complete site/catalog atomically.
See the website repository's [entry-point runbook](../broiler-platform.github.io/README.md#browser-app-entry-points).
The separate-site procedure below remains an alternative; it is not required
for the consolidated website configuration. The producer contracts remain shared.

Date: 2026-10-29. The artifact producer, integration and GitHub Pages deployment
implementation is ready for review. Live W6 acceptance remains pending the real
producer releases, published shared packages, domain configuration and the first
staged promotion/rollback exercise. No release, commit, push or deployment was
performed during this implementation.

## Three independent Pages sites

| Repository | Custom domain | Contents |
| --- | --- | --- |
| `Broiler-Platform/Broiler` | `demo.broiler.app` | Launcher releases, approved catalog, suite history |
| `Broiler-Platform/Broiler.Writer` | `writer.broiler.app` | Writer releases and receipts |
| `Broiler-Platform/Broiler.Cells` | `cell.broiler.app` | Cell releases and receipts |

The separate `broiler-platform.github.io` marketing repository is unchanged.
Read-only API inspection found an existing workflow-based Broiler Pages site at
`https://broiler-platform.github.io/Broiler/`, without a custom domain. The Writer
and Cells Pages endpoints returned 404. These observations do not configure DNS
or prove that an existing public demo is safe to replace. Review its current
content and Pages deployment history before the first deployment.

Each application lives at `/releases/<component-version>/`. Its files are the
producer's complete extracted `wwwroot`, with no rewriting of HTML, import maps,
JavaScript, runtime files, compression sidecars, fonts or media. The ZIP itself
is retained as the original GitHub release asset. Site receipts and catalogs
live outside those directories. Browser and Plate remain disabled.

The demo root loads `/office-web.json` and redirects to its exact Launcher
version. That Launcher uses the same approved catalog to select exact Writer and
Cell versions. Editor roots offer a link to the approved suite; staging a new
editor never makes it the default. Direct immutable editor URLs work without a
Launcher session or server.

## Producer contracts and workflows

Canonical producer tools are in `eng/web-producer`. Byte-identical copies are in
each producer's `eng/web` directory, including Launcher under `Broiler.Launcher`.
Repository-specific `producer.json` identifies the head, repository and version
property. Tools never infer a release version from an unrelated product.

The pinned cohort is SDK **10.0.403**, workload set **10.403.0**, and the Mono/
Emscripten manifest **10.0.114/10.0.100**. Initial output is trimmed interpreter
mode, with full AOT disabled. AOT is rejected by this contract.

Every release comprises:

* `Broiler.<Writer|Cells|Launcher>-<version>-browser-wasm.zip`;
* matching `.web-artifact.json`, containing source repository/tag/commit, resolved
  dependency versions, SDK/workload/runtime mode, archive size/SHA-256, generated
  module entry and the complete per-file size/SHA-256 inventory;
* matching `.SHA256SUMS.txt`.

Verification checks the ZIP identity and every member, rejects path traversal,
links, duplicate/case-colliding paths and oversized expansion, and validates the
generated entry, import-map references and SRI. Existing outputs cannot be
overwritten. The publisher requires a clean source tree and committed package
locks; `-VerificationOnly` explicitly marks local dirty builds as ineligible for
release. Integration and deployment reject that flag.

Writer/Cells have reusable `web-artifact.yml` workflows and independent
`publish-web.yml` entry points. Their existing full releases call the reusable
producer and attach its original bytes. Writer's legacy BOSS web component
metadata is retained; BOSS and standalone hosting can consume the same ZIP.
Cells' web descriptor is separate from native RID metadata. Launcher has
`broiler-launcher-web-artifact.yml` and `broiler-launcher-web-release.yml`.
Build jobs have read permissions; release attachment happens in separate jobs.

Standalone producer tags are `writer-web-v<version>`, `cells-web-v<version>` and
`launcher-web-v<version>`. Workflow inputs are passed through environment
variables, rather than interpolated into shell source. Browser tests boot the
extracted archives in Chromium, Firefox and WebKit before a draft is created.

The Launcher requires explicitly published W1 Hosting/Graphics versions and a
reviewed lock for those versions. Local W2 package fixtures cannot enter a
release workflow. Writer and Cell currently use the independent browser input
adapters and published Graphics preview.14. The contract records those as
`writer.browser/1`, `cell.browser/1` and `broiler.canvas/legacy-preview14`;
it does not mislabel them as the W1 shared host. W1 manifest-based replay is
`broiler.canvas/1`, with Launcher input `broiler.host/1`. Adapter version exports,
package identities and replay manifests are checked against the archive.

## Office lock and release ledger

`scripts/office_web.py` is the canonical artifact-only integrator. Deployment
copies plus its producer validator are vendored in each repository's
`eng/web-deploy`, allowing Pages jobs to run without application submodules,
NuGet, .NET or application compilation. Parity tests cover the copies.

The separate `office-web.lock.json` contract requires:

* schema 1, kind `broiler-office-web-lock`, status `locked`, independent suite
  version and `preview`/`stable` channel;
* exactly Launcher, Writer and Cell, with fixed repositories and allowed origins;
* exact component version, release tag, 40-character source commit, numeric
  GitHub release ID, archive and descriptor asset IDs/names/sizes/SHA-256;
* SDK, workload manifest/set, interpreter mode, replay/input protocol and the
  exact `https://<origin>/releases/<version>/` entry.

`lock` resolves real published releases, verifies the downloaded producer
archives, and resolves annotated tags to commits. No placeholder digest or
invented asset identity is accepted. The checked-in lock deliberately has
`pending-producer-releases` status and cannot deploy.

`health --output office-web-release.json` produces the separate schema-1
`broiler-office-web-release` ledger: suite identity, canonical lock digest,
exact component identities, disabled Browser/Plate entries and observed delivery
headers. It verifies every public file against the pinned producer inventory,
plus HTTPS origins and HTML/JS/JSON/CSS/WebAssembly MIME types. Files missing from
the manifest cannot be substituted through an HTML fallback page.

Each site publishes `/receipts/<component-version>.json`, binding its immutable
directory to its producer identity and inventory. Receipts are checked against
freshly verified producer descriptors, rather than trusted as the sole authority.
Approved ledgers live under `/ledgers/<catalog-version>.json`; immutable approved
catalogs live under `/catalogs/<catalog-version>.json`.

The Windows `office.lock.json`, native release contracts and RID validator are
unchanged. Desktop signing remains the separately deferred roadmap requirement.

## One-time Pages and DNS setup

1. Commit and push reviewed source/tool/workflow changes in all involved
   repositories. Publish W1 shared packages, select their real versions and
   commit the Launcher restore lock before making release-eligible builds.
2. In each repository's **Settings → Pages**, select **GitHub Actions** as source.
   Add its custom domain from the table above. The workflow writes a `CNAME`
   marker, but Actions-based Pages configuration still requires the repository's
   custom-domain setting; the file alone does not configure it.
3. At the DNS provider, create CNAME records for `demo`, `writer` and `cell`, each
   pointing to **`broiler-platform.github.io`**, without a repository/path suffix.
   Verify domain ownership in GitHub and remove conflicting records. Keep the
   marketing site configuration separate.
4. Wait for GitHub's DNS check and automatic certificate issuance, then enable
   **Enforce HTTPS** for all three sites. Browser HTTPS uses GitHub's certificates;
   it does not require the deferred desktop signing certificate.
5. Protect the `github-pages` environment with the desired reviewers and permit
   deployment from the reviewed branch. Workflows require contents write, Pages
   write and OIDC token permissions within their own repository. No cross-repo
   deployment token or Android signing secret is needed for public releases.
6. Retain the `web-pages` branch. It stores the complete static site, previous
   immutable versions and catalog history. The Actions source remains the
   reviewed development branch; `web-pages` is storage, not application source.

GitHub documents custom-domain settings and CNAME targets in
[Managing a custom domain](https://docs.github.com/en/pages/configuring-a-custom-domain-for-your-github-pages-site/managing-a-custom-domain-for-your-github-pages-site).
See also [HTTPS setup](https://docs.github.com/en/pages/getting-started-with-github-pages/securing-your-github-pages-site-with-https).

## Release, integration, staging and promotion

1. Run each producer's independent publish workflow, review its evidence and
   publish its draft GitHub release. These steps compile applications once.
2. In Broiler, run **Prepare Office web suite release**, supplying a new suite
   version and the three exact published producer tags. It creates a draft
   `office-web-v<catalog-version>` release containing the resolved
   `office-web.lock.json` and its checksum. Review those assets, then publish it.
   The equivalent local command is:

   ```powershell
   python scripts/office_web.py lock --catalog-version 0.1.0-preview.1 --launcher-tag launcher-web-v0.1.0-preview.1 --writer-tag writer-web-v0.1.0-preview.1 --cell-tag cells-web-v0.1.0-preview.1 --output artifacts/suite-review/office-web.lock.json
   ```

   Versions above are examples, not existing releases.
3. Run **Verify Office web suite** with the suite tag and reviewed lock asset
   SHA-256. It downloads and verifies released bytes, extracts complete closures,
   runs three-engine application smoke tests and three-origin catalog/navigation/
   Return to Office tests, then uploads `office-web-integration-evidence`.
   The three-origin harness intercepts HTTPS requests locally; it cannot approve
   production or claim live-domain acceptance.
4. Run **Office web Pages**, operation **stage**, in Writer, Cells and Broiler,
   each with that same suite tag and lock SHA-256. The jobs retain prior versions,
   reject version/receipt collisions, add new immutable content, and deploy their
   whole retained sites using Pages artifacts. No product is rebuilt. Existing
   approved catalogs remain unchanged.
5. Check the staged HTTPS application URLs and the current live demo. For a
   standalone server-side check:

   ```powershell
   python scripts/office_web.py health --lock artifacts/suite-review/office-web.lock.json --output artifacts/suite-review/office-web-release.json
   ```

6. Read the current `https://demo.broiler.app/office-web.json` bytes and calculate
   their SHA-256; use `none` only if no approval pointer exists. Run **Office web
   Pages** in Broiler with operation **promote**, the suite tag/lock digest,
   `expected_previous_sha256`, and the successful integration run's numeric ID.
   Promotion verifies the integration workflow's success, deployment-code commit,
   exact lock bytes and all four three-engine reports. It then rechecks every
   live file, writes immutable catalog/ledger history and updates the single
   approved pointer last. A stale previous digest aborts promotion.
7. Visit the live demo and both exact editor URLs in the supported browsers.
   Confirm displayed versions, navigation, direct visits, MIME/TLS and editing.
   Retain the suite lock, run ID and deployed ledger with release evidence.

The site branch commit can precede a failed Pages deployment. Such a failure
does not establish live approval: retry the same reviewed operation, then verify
the public pointer and files. Deployments are serialized per repository and
pushes are ordinary non-force pushes. A site size guard stops before 900 MiB;
review capacity before reaching GitHub's
[1 GB published-site limit](https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits).

## Rollback

Select the previous published suite release and reviewed lock digest. Run its
integration on the current deployment code, then use Broiler's **promote**
operation with that previous suite and the SHA-256 of the currently approved
pointer. Health checks still verify all three prior immutable directories.
Only the catalog pointer returns to its prior content; no application rebuild,
archive rewrite, native installation or editor-origin deployment is required.
Existing open tabs keep their loaded version until navigation/reload.

Do not delete immutable directories used by approved or rollback catalogs.
Browser recovery remains origin-local and is neither migrated nor shared by
deployment. W7/W8 must separately cover future storage-schema and offline-update
compatibility.

## Pages delivery limits

Pages controls response cache/compression and HTTP security headers. This
implementation preserves `.br`/`.gz` files but does not claim automatic sidecar
negotiation. It serves the normal entry/runtime files and records observed
cache/security headers. Immutable version URLs prevent mixed runtime closures;
the root router and Launcher request mutable catalogs without using their local
cached copies. Pages/CDN propagation can still delay promotion/rollback.

Custom `Cache-Control`, CSP, Permissions-Policy, COOP/COEP or framing headers
cannot be configured by an application `_headers` file on GitHub Pages. Do not
claim thread support, immediate CDN invalidation or those protections from this
deployment. The current interpreter mode needs no threaded cross-origin-isolated
runtime. If stricter header/caching guarantees become release requirements, use
an edge service capable of setting them, or revisit hosting before that release.
W7 remains responsible for full accessibility/security/browser acceptance.

## Verification and remaining live gates

* 25 producer/deployment tests passed, including real ZIP extraction through
  staged publishing, tamper/path/SRI/cohort failures, release-shell attachment
  byte preservation, immutable collisions, health failures, pointer concurrency,
  catalog-only rollback and credential stripping on asset-host redirects.
* 14 Launcher browser-service/catalog unit tests passed, including exact version
  destination validation.
* Actionlint passed for root and both producer workflow sets.
* Actual local Writer and Cell producer publishes, packaging and re-extraction
  passed with explicit protocol metadata. Launcher was rebuilt using the W2
  local-package cohort, packaged and re-extracted as an explicitly ineligible
  verification archive. These are verification-only outputs, not release assets.
  Nine final standalone browser smoke cases passed across the three engines.
* The updated Launcher was also published using the isolated W2 local package
  cohort for the versioned-catalog test. All three engines passed the three-origin
  suite flow against this build and the extracted Writer/Cell verification
  archives. A normal persistent profile is used, matching W5; private-profile
  acceptance remains W7 work.

Evidence is under `artifacts/web-w6-1`, including `suite-final-smoke.json`, producer
archives/descriptors, screenshots and build logs. Local verification fixtures
are never accepted as a production web lock.

W6's live exit criterion is still open: publish the missing W1 package cohort,
commit and publish real producer assets, configure the Pages domains/DNS/HTTPS,
then execute the reviewed integration, first deployment and rollback drill.
The checked-in pending lock deliberately prevents claiming that these already
happened. W3's separately recorded Firefox RTF acceptance issue and W7 quality
work are not closed by deployment smoke tests.
