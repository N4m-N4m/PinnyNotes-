# Code signing with SignPath Foundation

Pinny Notes releases can be signed for free through [SignPath Foundation](https://signpath.org), which offers code signing to open source projects. Signed builds show a verified publisher and stop most "Windows protected your PC" (SmartScreen) warnings.

The release workflow is already set up for it. It stays unsigned until the steps below are done.

## 1. Apply

1. Make sure two-factor authentication is on for every GitHub account with write access to this repo. SignPath requires it.
2. Apply at <https://signpath.org/apply> with:
   - **Repository:** https://github.com/N4m-N4m/PinnyNotes-
   - **Licence:** GPL-2.0
   - **Build system:** GitHub Actions on GitHub-hosted runners, see `.github/workflows/release.yml`
   - **Artifacts:** `.exe`, `.zip` (contains the exe) and `.msi` for x64 and ARM64
   - **Code signing policy:** the "Code signing policy" section of the README
3. Wait for approval. SignPath Foundation reviews each project by hand, so this can take a while.

## 2. Configure SignPath

Once approved you get access to an organisation at <https://app.signpath.io>.

1. **Connect GitHub:** install the SignPath GitHub App on this repository and add a trusted build system for GitHub.com to the project.
2. **Project:** use the slug `PinnyNotes`. Any other slug also works if you set the `SIGNPATH_PROJECT_SLUG` variable to match.
3. **Artifact configuration:** paste in `.signpath/artifact-configuration.xml` and make it the default.
4. **Signing policy:** use `release-signing` with the Foundation's certificate. Any other slug also works if you set `SIGNPATH_SIGNING_POLICY_SLUG` to match.
5. **API token:** create a CI user, add it as a submitter on the signing policy, and generate an API token for it.

## 3. Add the secrets to GitHub

Go to repo **Settings → Secrets and variables → Actions** and add:

| Kind | Name | Value |
| --- | --- | --- |
| Secret | `SIGNPATH_API_TOKEN` | The CI user's API token |
| Variable | `SIGNPATH_ORGANIZATION_ID` | Your organisation ID (shown in SignPath under Settings) |
| Variable (optional) | `SIGNPATH_PROJECT_SLUG` | Only if it isn't `PinnyNotes` |
| Variable (optional) | `SIGNPATH_SIGNING_POLICY_SLUG` | Only if it isn't `release-signing` |

From the next tag on, the workflow sends the built files to SignPath and waits for them to be signed. If the policy requires manual approval, an approver has to approve each request in SignPath before the release is published. The workflow then publishes the signed files and their checksums.
