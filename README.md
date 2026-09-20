# Project C.A.R.E - RHU Vinzons

VB.NET (WinForms, .NET 10) desktop app + MySQL backend implementing the
full patient-care workflow from the project proposal: login, dashboard,
patient registration/directory, family/household linking, the encounter ->
triage -> consultation flow (diagnoses + prescriptions), the lab-request ->
result-release and prescription -> dispensing loops against real stock
batches (FEFO) with stock/expiry monitoring, staff account + role/
permission administration, all 10 report templates with CSV export, and
medical certificate issuance with printing. Every sidebar item from the
original proposal is now built.

See **[ARCHITECTURE.md](ARCHITECTURE.md)** for how the pieces actually talk
to each other: the ID-passing-callback pattern between screens, the global
session, the `Db`/`DbTransaction` data-access layer, and a full traced
example from patient registration through to a lab request.

## 1. Set up MySQL (XAMPP)

1. Start Apache + MySQL in the XAMPP control panel.
2. Import the schema, then the seed data, either via phpMyAdmin's Import
   tab or the command line:
   ```
   mysql -u root -p < database/schema.sql
   mysql -u root -p < database/seed_data.sql
   ```
   (XAMPP's default root user has no password — just press Enter.)

This creates the `rhu_care` database with all 17 tables and:
- 6 roles matching the proposal's User Levels table
- 6 demo logins (`admin`, `management`, `records`, `healthcare`, `pharmacy`,
  `laboratory`), all with password **`Admin@123`**
- 11 barangays referenced in the proposal's own mockups
- 5 starter medicines with one stock batch each

### Optional: bulk test/demo data (~10,000 rows per table)

```
mysql -u root -p rhu_care < database/seed_bulk_data.sql
```

Adds ~10,000 rows each to households, patients, encounters, consultations,
diagnoses, prescriptions, dispensing, lab requests, referrals, medical
certificates, stock batches, and audit logs — real volume to test search,
reports, and dashboard performance with. Also tops up the medicine catalog
(~30 medicines) and staff (~40 accounts across the 6 roles, same
`Admin@123` password). Roles and barangays are deliberately left as-is —
the app puts them straight into dropdowns, and 10,000 rows there would
make those unusable, not just slow. Every generated code carries a
`-BULK-` marker (`PT-BULK-000123`, `ENC-BULK-000456`, ...) so it's obvious
which records are demo data versus what you enter through the app. Takes
a few minutes to run; safe to re-run schema.sql + seed_data.sql +
seed_bulk_data.sql from scratch any time you want a clean slate.

## 2. Run the app

```
dotnet run
```

On first launch the app writes `dbconfig.ini` next to the executable with
XAMPP's defaults (`localhost`, port `3306`, database `rhu_care`, user
`root`, no password). Edit that file if your MySQL setup differs.

Sign in with any demo account above. The sidebar only shows the modules
that account's role has access to (see `roles.module_access` in
`database/seed_data.sql`).

## Project layout

- `database/schema.sql` — full MySQL schema (roles, users, barangays,
  households, patients, encounters, consultations, diagnoses,
  prescriptions, pharmacy/medicines, laboratory, referrals, documents,
  audit logs)
- `database/seed_data.sql` — demo roles/users/barangays/medicines
- `Data/` — `Db.vb` (ADO.NET helper), `DbConfig.vb` (connection string),
  `IdGenerator.vb` (PT-/ENC-/HH-/RX- codes), `AuditLogger.vb`
- `Security/PasswordHasher.vb` — PBKDF2 password hashing
- `Models/Session.vb` — logged-in user + role permissions for the session
- `Forms/` — `LoginForm`, `MainDashboardForm` (shell + nav), and each
  module screen:
  - Laboratory: `LaboratoryForm` (worklist), `LabRequestDialog` (opened from
    the encounter workspace's "Request Lab" button), `LabResultDialog`
    (enter/release results), `Data/LabTestCatalog.vb` (test list)
  - Pharmacy: `PharmacyForm` (pending prescriptions + dispensing history),
    `DispenseMedicationDialog` (picks the earliest-expiring batch, FEFO)
  - Inventory: `MedicineInventoryForm` (catalog + batches),
    `AddMedicineDialog`, `ReceiveStockDialog`
  - Admin: `UserManagementForm` + `AddEditUserDialog` (create/edit/
    activate/deactivate staff accounts), `RolesPermissionsForm` +
    `NewRoleDialog` (module access + add/edit/delete/view flags per role,
    via `Data/ModuleCatalog.vb`)
  - Reports: `ReportsForm` (shell + Master List + Family Folder) and its
    partial-class siblings `ReportsForm.PharmacyReports.vb` (Inventory
    In/Out, Prescription, Patient Medication Profile),
    `ReportsForm.ClinicalReports.vb` (Consultation, Morbidity, Case
    Finding, Treatment), `ReportsForm.AuditReport.vb` (Audit Logs); every
    report's "Export ▾" button opens a menu for CSV (`Data/CsvExporter.vb`),
    PDF (`Data/PdfExporter.vb`, via QuestPDF — RHU letterhead + one section
    per table shown on screen), or Excel (`Data/ExcelExporter.vb`, via
    ClosedXML — one worksheet per section). Master List gained Sex/Status
    filters, Prescription/Consultation gained a name search box, Inventory
    gained a medicine-name filter, Audit Logs gained a user filter.

### Search performance (at 10,000+ rows)

- Every search box across the app (Patient Directory, Encounters, Lab,
  Pharmacy, Certificates, User Management, Family/Household) is debounced
  via `UiTheme.AttachDebouncedSearch` — typing no longer fires a query per
  keystroke, only once typing pauses (~350ms).
- Every list screen caps its default result set at `UiTheme.ResultCap`
  (300) and shows a "type to search for more" hint when the cap is hit,
  instead of loading the whole table the moment the screen opens.
- The patient/household pickers that used to preload every row into a
  ComboBox (Patient Medication Profile report, Family Folder report,
  Family/Household's "link patient" combo, Issue Certificate's patient
  picker) now use `UiTheme.AttachSearchableCombo` — type at least 2
  characters and it queries the database for a bounded (20-row) match
  instead of populating a 10,000-item dropdown.
  - Medical Certificates: `MedicalCertificatesForm` (browse/issue),
    `IssueCertificateDialog` (also opened from the encounter workspace's
    "Medical Certificate" button), `CertificateViewDialog` (formatted
    preview + real printing via `System.Drawing.Printing`)

## Known limitations / next phase

Every module from the original proposal is now built. Remaining rough
edges:

- Case Finding and Treatment Report (proposal Figures 39-40) are DOH-style
  TB/HIV intake forms with fields this schema doesn't have a home for
  (screening mode, bacteriological status, regimen, HIV testing, etc.).
  The built reports are a simplified substitute that surface patients
  whose recorded diagnosis text mentions TB/Tuberculosis/HIV — not a full
  implementation of those forms.
- The Inventory In/Out report's "On Hand" column is the medicine's
  *current* stock, not its balance at the end of the selected date range —
  there are no historical daily stock snapshots to compute that from.
- CSV export only ever writes the first table shown (CSV has no concept of
  multiple sheets/sections); reports with more than one grid on screen
  (Family Folder, Inventory In/Out, Morbidity) export all of them in PDF
  and Excel, but only the first in CSV.
- QuestPDF is used under its free Community license (see
  `ApplicationEvents.vb` / questpdf.com/license) — fine for a school
  project or small team, but re-check that license page before any
  larger-scale/commercial deployment.
- Role/permission changes only take effect for a user's *next* login — the
  logged-in session caches its permissions in `Models.Session` at sign-in
  time (`RolesPermissionsForm` warns about this when you edit your own
  role).
- Prescriptions match stock batches by exact medicine name (no shared FK
  between `prescriptions_medication_orders` and `medicines`); dispensing a
  medicine that was typed differently at prescribing time than in the
  catalog will show "no tracked stock batches" and dispense without a
  stock deduction.
- `IdGenerator` counts existing rows per year to build codes like
  `PT-2026-0001`; fine for single-workstation RHU use, not safe under
  concurrent multi-writer inserts.
- The demo accounts all share one password — change them (or add a
  "change password" screen) before any real deployment.
- Medical certificates must be tied to an encounter (`other_documents_
  services.encounter_id` is `NOT NULL`) — a patient with no encounters yet
  can't have a certificate issued until one is started. Printing uses the
  standard Windows print dialog (no printer configured just shows a
  friendly error and leaves the text visible to copy); there's no separate
  PDF export, only whatever "Print to PDF" driver the machine has.
