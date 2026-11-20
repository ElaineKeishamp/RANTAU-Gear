# Project guidance

This is RANTAU Gear, a VB.NET Windows Forms application targeting .NET 10.

- Preserve Option Strict On and the IRentalRepository boundary.
- Apply the installed antislop core, UI, human, and copywriting skills for interface work. Read DESIGN.md for the project's design data. The current task uses DURING mode; resolve the mode normally in future sessions.
- Codebase Memory MCP is already connected. Use its project index when helpful, but inspect VB.NET source directly where symbol coverage is absent.
- Supabase integration is in progress. Read docs/BUSINESS_RULES.md and docs/SUPABASE_SETUP.md before changing online contracts. The forms still use DummyRepository until the async adapter is integrated. Do not insert credentials, pretend the demo is online, or ship demo passwords as production authentication.
- EXE distribution/publishing is deferred. A normal debug build for validation is allowed.
- User output location: save generated artifacts, exports, screenshots, backups, and configurable task temporary/cache files only under `D:\elaine\SEM 5\Pemrograman Visual-backup`. Do not write task outputs to drive C. Reading existing installed tools or skills on C is allowed. Keep edits to existing project source in this workspace. Request filesystem escalation if writing the output folder requires it; do not silently use a different location.
- Build: dotnet build RANTAUGear.slnx. Run the application to check layout and role-specific navigation.
