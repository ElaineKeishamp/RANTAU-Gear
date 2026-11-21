# Project guidance

This is RANTAU Gear, a VB.NET Windows Forms application targeting .NET 10.

- Preserve Option Strict On and the IRentalRepository boundary.
- Apply the installed antislop core, UI, human, and copywriting skills for interface work. Read DESIGN.md for the project's design data. The current task uses DURING mode; resolve the mode normally in future sessions.
- Codebase Memory MCP is already connected. Use its project index when helpful, but inspect VB.NET source directly where symbol coverage is absent.
- Supabase integration is in progress. Read docs/BUSINESS_RULES.md and docs/SUPABASE_SETUP.md before changing online contracts. Primary login, online reads, and master-data editors use SupabaseRentalRepository through the async IRentalRepository members. DummyRepository and synchronous members remain for explicit test/demo previews only; Master writes use save_master_data with request UUID and optimistic row_version; Reservations and received DP use preview_reservation/reserve_with_dp through async repository methods. Pickup, deposit and return/settlement UI are deferred. Confirm actual receipt and exact server quote; reuse the same request UUID/payload only for retry. Do not insert credentials, pretend the demo is online, or ship demo passwords as production authentication.
- EXE distribution/publishing is deferred. A normal debug build for validation is allowed.
- User output location: save generated artifacts, exports, screenshots, backups, and configurable task temporary/cache files only under `D:\elaine\SEM 5\Pemrograman Visual-backup`. Do not write task outputs to drive C. Reading existing installed tools or skills on C is allowed. Keep edits to existing project source in this workspace. Request filesystem escalation if writing the output folder requires it; do not silently use a different location.
- Build: dotnet build RANTAUGear.slnx. Run the application to check layout and role-specific navigation.
