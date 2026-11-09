# Self-Hosted Runner Checklist (SunhavenMod)

Use this checklist before enabling self-hosted build workflows.

Compile jobs in [`build-mods.yml`](../.github/workflows/build-mods.yml) and [`release-publish.yml`](../.github/workflows/release-publish.yml) run on a **Linux** runner with labels `self-hosted`, `Linux`, `X64`, `sunhaven`. There is no Windows runner compose file in this repository (`.github/self-hosted-runner/docker-compose.windows-runner.yml` does not exist). Local .NET SDK in Docker is a separate path: [`docker-compose.dotnet-sdk.yml`](../docker-compose.dotnet-sdk.yml) — see [`DOCKER_DOTNET_CONTAINER.md`](DOCKER_DOTNET_CONTAINER.md).

## Host Setup

- Linux host registered as a GitHub Actions self-hosted runner.
- Runner labels: `self-hosted`, `Linux`, `X64`, `sunhaven`.
- GitHub repository admin access available for runner registration.

## Required Host Folders

Workflows pass these as MSBuild properties (game mount is treated as read-only; `SunhavenCopyToBepInExPlugins=false`):

- `/sunhaven/BepInEx` (`BEPINEX_PATH`)
- `/sunhaven/Sun Haven_Data/Managed` (`MANAGED_PATH`)

## Copy Sun Haven References

Copy required managed references from a Sun Haven install into the runner mounts above.

Minimum required DLLs in the Managed folder (same set as [`scripts/build/check-sunhaven-refs.ps1`](../scripts/build/check-sunhaven-refs.ps1)):

- `Assembly-CSharp.dll`
- `BepInEx.dll`
- `UnityEngine.CoreModule.dll`
- `UnityEngine.dll`
- `Unity.TextMeshPro.dll`
- `mscorlib.dll`
- `System.dll`
- `System.Core.dll`

## Start and Validate

- Confirm the runner is Online on the GitHub Actions Runners page with the `sunhaven` label.
- Run preflight against a local Sun Haven (or refs) root:
  - `pwsh ./scripts/build/check-sunhaven-refs.ps1 -SunHavenPath "<path>"`

## Pre-Release Validation

- Run version consistency check:
  - `python3 scripts/version/verify-version-consistency.py`
  - or `pwsh ./scripts/version/verify-version-consistency.ps1`
- Run build script for one mod:
  - `pwsh ./scripts/version/pre-push-build.ps1 -Mod senpaischest -BuildOnly`
- Confirm output DLL exists in expected build output location.

## Ongoing Maintenance

- Refresh `/sunhaven` refs after game updates.
- Keep the runner and .NET SDK 8 updated periodically.
