# AGENTS.md

## Branding Rule (read this first)
ClassFabric is a **surface-only brand** on top of the ClassIsland codebase, reverted to upstream-real names since 2.1.1.4:

- Project directories, project files, assembly names, namespaces, avares:// URIs, the `classisland.tech` XAML schema URLs, the `classisland://` URI scheme, data directories (`%APPDATA%\ClassIsland\Data`, `%LOCALAPPDATA%\ClassIsland`) and asset file names all use the upstream-real `ClassIsland.*` names. Do not introduce `ClassFabric` into any of these.
- The only code-level exception is the desktop executable name: `ClassIsland.Desktop` builds into `ClassFabric.Desktop.exe` via an explicit `<AssemblyName>` in its csproj, plus the installed executable `ClassFabric.exe` referenced by the installer and `App.axaml.cs`. Do not "fix" these to ClassIsland — deployment images and existing shortcuts depend on them.
- `ClassFabric` remains correct in user-visible copy (resx strings, dialogs, titles), installer metadata (`AppName`, `Product`, `Title`), the Inno AppId family (`cn.classfabric.app`, `FClassFabric_*` artifacts), the Phainon distribution paths (`classfabric/distribution-v2/...`), the SignPath project slug, release asset names, and the repository name `Ansoukin/ClassFabric`.
- After rename-sensitive work, review `git grep -I -i "ClassFabric"` and confirm every remaining hit falls into one of the retained categories above.

## Repo Shape
- Main solution: `ClassFabric.sln` (file name kept intentionally); platform-filtered solutions: `ClassFabric.Filter.Linux.slnf`, `ClassFabric.Filter.MacOs.slnf`.
- Runtime app entrypoint: `ClassIsland.Desktop/Program.cs`; Avalonia app/library: `ClassIsland/`; shared UI/services: `ClassIsland.Core/`.
- Android entry project: `ClassIsland.Android/`; it targets `net10.0-android` and shares the Avalonia application code from `ClassIsland/`.
- Platform services: `platforms/ClassIsland.Platforms.{Windows,Linux,MacOs}`, wired by `CrossPlatformProps.props`.
- `ClassIsland.Shared` and `ClassIsland.Shared.IPC` target `net10.0;net472` on Windows and `net10.0` elsewhere; avoid APIs unavailable on `net472` in shared code.
- Most app projects target `net10.0`; `ClassIsland.Launcher` intentionally remains on `net9.0`.
- Upstream-market plugins load directly against the `ClassIsland.*` assemblies; there is no compatibility shim layer anymore.

## SDK And Restore
- `global.json` requests the .NET 10 SDK with `rollForward: latestFeature` and prerelease SDKs allowed. Use a .NET 10 SDK for repository builds, including the `net9.0` launcher.
- The UI stack currently uses Avalonia `12.1.1` and FluentAvalonia `3.0.0`; check `AvaloniaShared.props` and project package references before documenting or using version-specific APIs.
- Android builds require the .NET Android workload; macOS builds require the matching macOS workload and Xcode toolchain.
- Repo depends on `vendors/EdgeTtsSharp` (`classisland-v2` branch). If missing, run `git submodule update --init --recursive`.
- CI uses GitHub Package Registry: `https://nuget.pkg.github.com/ClassIsland/index.json`.

## Build Commands
- Desktop app: `dotnet build ClassIsland.Desktop/ClassIsland.Desktop.csproj -c Debug`
- Android app: `dotnet build ClassIsland.Android/ClassIsland.Android.csproj -c Debug` after installing the Android workload.
- Single library: `dotnet build ClassIsland.Core/ClassIsland.Core.csproj -c Debug`
- Use NUKE only for release/publish builds, not verification.
- Release packaging: `./build.ps1 PublishApp` (Windows) or `./build.sh PublishApp` (Unix) with required metadata.
- `PublishApp` requires metadata: `--OsName windows|linux|macos`, `--Arch x64|x86|arm64`, `--Package folder|deb|pkg`, `--BuildType full|selfContained`, `--BuildName appBase|app`, `--AppVersion <version>`.
- Android release packaging uses `./build.sh PublishAndroidApp --OsName android --Arch arm64 --Package apk --BuildType monoaot --BuildName app --AppVersion <version>`; production branding additionally passes `--IsProductionBuild true`.
- Launcher packaging: `PublishLauncher` (native AOT self-contained).
- Plugin dev environment target `InitPluginDevEnv` writes user environment variables/profile blocks pointing at `out/ClassIsland_Dev`; do not run it casually during verification.

## Agent Workflow
Before editing:
1. Inspect related existing code.
2. Identify the current design pattern.
3. Explain the planned changes.
4. Only then modify files.
Avoid speculative refactoring.
- Generated code must follow the repository's formatting settings; do not generate minified or compressed code.
- Keep implementations and fixes as small and concise as possible, without affecting other behavior. If effects on other behavior are unavoidable, inform the user before making the change.
When requirements are unclear:
- Ask the user when changes affect architecture, behavior, compatibility, or public APIs.
- For minor implementation details, follow existing project patterns.

## Priority Rules
When instructions conflict, follow this order:
1. Preserve existing architecture and behavior.
2. Keep changes minimal and focused.
3. Prefer existing patterns over introducing new abstractions.
4. Do not modify unrelated files.
5. Ask before making large architectural changes.

## Compatibility Rules
- Preserve existing public APIs whenever possible.
- Avoid breaking API changes unless explicitly required.
- If an API change is necessary:
  - Explain the reason.
  - Identify affected consumers.
  - Inform the user before making the change.
  - Consider backward-compatible alternatives or migration paths.

### Naming Is Load-Bearing
Never run a blanket rename of `ClassIsland` to `ClassFabric`. The brand lives only on the surface; the code tree must stay on upstream-real names.

- Do not rename or remove: upstream repository and documentation links, upstream dependency package IDs (`ClassIsland.SimpleGitInfoGenerator`, `ClassIsland.Markdown.Avalonia`, …), the GitHub Package Registry source `https://nuget.pkg.github.com/ClassIsland/index.json`, the `vendors/EdgeTtsSharp` branch name, licence and copyright headers, or the ClassIsland v1 data-import symbols (`ClassIsland1ImportProvider`, `ClassIslandV1ProfileTransferHelper`).
- Types under `ClassIsland.Core.*` are plugin-visible API. Never add an optional parameter to an existing constructor: that creates a new signature, and already-compiled plugins throw `MissingMethodException` while their attributes are parsed. Add an overload or a separate attribute instead.
- resx embedded resource names derive from `<RootNamespace>`; the main project pins `ClassIsland`. Changing it desyncs the `Localization.*.Designer.cs` managers and crashes startup with `MissingManifestResourceException`.

## Platform Targeting
- `CrossPlatformProps.props` auto-selects platform constants from host OS for normal builds; release builds override this with `PublishBuilding=true` and `PublishPlatform=<os>`.
- `ClassIsland.Desktop` conditionally references one platform based on `Platforms_Windows`, `Platforms_Linux`, `Platforms_MacOs`.
- Linux requires X11; Wayland/XWayland not supported.

## Generated And Output Files
- NUKE artifacts: `out/`; plugin builds: `.cipx` files.
- `ClassIsland/secrets.g.cs` is generated by NUKE `GenerateSecrets` and deleted by `PostCleanup`; never commit.
- Ignore `*_wpftmp.csproj` files.
- CsWin32 inputs: `NativeMethods.txt`/`NativeMethods.json`.

## Changelog And Release CI
- Release notes live at `doc/ChangeLogs/<primary_version>/<release_tag>/App.md` (for example `doc/ChangeLogs/2.2/2.1.1.1/App.md`). The release workflow copies that file into the GitHub Release body, so a release fails if it is missing.
- Releases are produced by `.github/workflows/build_release.yml`, never created by hand.
- Any push triggers a full build through that workflow. Unless a full build is actually wanted, cancel the push-triggered run after pushing.
- The workflow accepts `workflow_dispatch` inputs including `release_tag`, `primary_version`, `is_test_mode`, `publish_only`, `source_run_id`, `release_name`, `is_prerelease`, `source_ref` and `is_draft`.
- Versions are derived from git tags; never create tags whose name is not a plain `major.minor.build.revision` version (a `backup/...` tag once broke every build with CS7034). Use branches for backup points.

## Remotes
- `origin` is `Ansoukin/ClassFabric`: this fork, and the only remote anything is ever pushed to. `upstream` is `ClassIsland/ClassIsland`, the parent project, kept read-only for tracking it. Never push to `upstream`.
- `gh` resolves this checkout to `upstream`, so pass `-R Ansoukin/ClassFabric` to every `gh` command. Without it you read the parent project's runs, releases and issues and silently miss this fork's own; a push-triggered Release run has already been missed that way once.

## Tests And Verification
- No test projects in the solution; do not add one unless explicitly asked. Verify with `dotnet build <project>` instead.
- After code changes, run at least one compile check; for app changes, use `dotnet build ClassIsland.Desktop/ClassIsland.Desktop.csproj -c Debug` rather than NUKE.
- If automated tests become available, run the relevant automated tests too.
- If automated testing is not available, tell the user what related behavior still needs manual verification.
- For platform-specific changes, build with intended host/platform settings; a Windows-host debug build will not compile Linux/macOS-specific code unless invoked through publish properties.
- `X`AML compile caches can hide XAML errors behind a green build: when XAML-related symptoms appear, delete all `obj/`/`bin/` directories and rebuild clean before trusting the result.

## Verification Rules
Before claiming work is complete:
1. Run the relevant verification command.
2. Read the full output.
3. Report results based on actual verification.
When verification is unavailable:
- Clearly state what was checked.
- Clearly state what could not be verified.
- Do not present assumptions as confirmed results.

## Debugging Rules
When encountering any bug, test failure, or unexpected behavior:
1. Read error messages carefully.
2. Reproduce the issue when possible.
3. Check recent changes.
4. Trace data flow and identify the root cause before fixing.
Avoid:
- Making changes based only on assumptions.
- Applying multiple unrelated fixes at once.
- Changing code without a clear hypothesis.
Red flags — STOP and follow process:
- "Quick fix for now, investigate later"
- "Just try changing X and see if it works"
- Proposing solutions before tracing data flow

## Avalonia Development Rules
1. Search first for existing patterns.
2. Follow MVVM and the existing CommunityToolkit.Mvvm (`ObservableObject`/`ObservableRecipient`) and DynamicData patterns.
3. Composition over inheritance.
4. Keep ViewModels platform-independent.
5. Avalonia `avares://` asset paths are case-sensitive: a path that differs from the file on disk only by letter case fails at runtime, not at compile time. This has already caused a startup crash once, so check the exact case of every asset path you add or change.

## User-Facing Diagnostic UI
- Write troubleshooting, recovery, configuration-error, and diagnostic UI that is accessible to ordinary users from the user's perspective, with clear outcomes and actionable next steps.
- Keep primary copy focused on what happened, how it affects the user, and what the user can do. Do not expose exception types, stack traces, internal identifiers, protocols, implementation names, or other technical details that the user does not need.
- If technical data is genuinely useful for support, place it behind a copy, export, or advanced-details action instead of making it the primary message.
- Explicitly developer-only surfaces such as DevPortal and internal diagnostic tools may use developer-oriented terminology.

## Icons
- For new or modified in-app `FontIcon`-style icons, prefer Fluent System Icons.
- Search the generated `ClassIsland.Core.Icons.FluentIcons` constant pool and choose the icon whose name most closely matches the intended action or concept. Do not guess glyphs or introduce raw Unicode/code-point literals such as `&#x...;`.
- Use the named `FluentIcons` constant from C# or XAML rather than duplicating its glyph value.
- Fall back to an existing alternative icon system only when Fluent System Icons has no suitable semantic icon or a platform convention explicitly requires another icon family.

## Anti-Patterns
Do NOT:
- Skip design for "simple" tasks.
- Add comments that only explain what the code already expresses.
- Use `this.` qualifier.
- Claim completion without verification.
- Fix symptoms without root cause.
Comments should explain intent, constraints, workarounds, or non-obvious decisions.
When unsure: ask the user.

## Contribution Conventions
- **Ask before committing or pushing.** Before running `git commit` or `git push`, use the Ask user input tool (`request_user_input`) to obtain the user's approval. The question must state the proposed Conventional Commit type (such as `feat` or `fix`) and a one-line summary of the change, so the user can confirm or revise them before anything is committed or pushed. If that tool is unavailable in the current session, ask in plain text and wait for the reply. Never commit or push unilaterally — not even for small, already-verified changes, or for changes the user explicitly asked for.
- **Describe the change, not the project plan.** A commit message must state what actually changed in the code or assets, in one line. Never write milestone, phase or progress language such as `finish W1 phase`, `M2 wrap-up`, `stage commit`, or `proceeding as planned`: it tells a future reader nothing about the change, and it leaks an internal roadmap into permanent history. Prefer `feat(api): add AutomationCategoryAttribute and category fallback chain` over `feat: finish W0 metadata groundwork`.
- Use Conventional Commits with scopes from `doc/Contributing/Scopes.md`.
- Feature work → `master`; fixes → maintenance branch.
- Before writing commit messages, read `doc/Contributing/Scopes.md` to get valid scope names.
- Before contributing code, read `CONTRIBUTING.md` to understand contribution guidelines.
- Commit as `Ansoukin <caisenfull@outlook.com>`, as both author and committer.
- Never rewrite history, force-push, or delete branches.

## Release Packaging
Only when explicitly requested:
```bash
./build.ps1 PublishApp --OsName windows --Arch x64 --Package folder --BuildType full --BuildName appBase --AppVersion 0.0.0.0
```
