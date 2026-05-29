# Nedoladené – vrátime sa neskôr

Poznámky k veciam, ktoré **fungujú**, ale ešte nie sú na 100 %.  
Pri práci na inej úlohe sa tu **nezdržujeme** – len zapisujeme, aby sme na to nezabudli.

**Ako používať:** pridaj riadok do tabuľky (alebo novú sekciu). Pri dokončení presuň do „Hotové“ na konci alebo riadok vymaž.

---

## Aktívny backlog

| Priorita | Oblasť | Čo treba doladiť | Kde / poznámka |
|----------|--------|------------------|----------------|
| vysoká | Import Excel | **Rozpoznávanie dielcov a určovanie zostáv** – heuristiky z názvu (číslo v „bok 11“, koncové číslo atď.) nie vždy sedia s reálnymi projektami; niekedy zlá zostava alebo „Bez zostavy“. | `Import/ExcelImporter.cs` → `ParseZostavaFromName`, `NameVariantExpander`; overiť na reálnych exportoch z Maestra |
| stredná | Import Excel | **Typ dielca (PartKind)** z názvu – bok L/P, dno, traverza, polica… môže byť nepresný pri neštandardných názvoch. | `Import/PartTypeHinter.cs`, `ExcelImporter.cs` |
| stredná | Operácie | **Editácia v paneli Operácie** – popup len pre `DrillOperation`; ostatné typy (Obeh, skrutky, …) zatiaľ nie. | `Views/OperationsPanel.xaml.cs` |
| nízka | Ribbon | **Skrutky, Drážka, Kovania** – tlačidlá vypnuté, funkcionalita chýba. | `MainWindow.xaml` |
| nízka | Nastavenia | **Šablóny operácií** – časť parametrov „Pripravujeme“. | `Views/OperationSettingsWindow.xaml.cs` |
| nízka | UI okien | **Vlastný title bar** (úplné odstránenie systémového pásu) – zatiaľ len tmavý DWM title bar. | `Interop/WindowThemeHelper.cs` |
| nízka | Zostava 3D | **Ťahanie výšky** – MVP cez UnProject na os X; nie presný pick dielca, kolízy, rotácia. Po pustení snap 32 mm; treba „Prepocítaj zostavu“. | `Views/Assembly3DWindow.xaml.cs` |
| nízka | Zostava | **Export zostavy** – výber priečinka cez uloženie jedného súboru; nie klasický folder picker. | `AssemblyViewModel.ExportAssemblyCommand` |

---

## Nápady / otázky (bez termínu)

- Pravidlá propagácie operácií medzi dielcami v zostave – overiť na zložitých zostavách (viac bokov, viac polic).
- Traverza – edge cases pri editácii kolíkov po návrate k dielcu (pattern/poloha vs. uložené hodnoty).
- Export XCS – validácia výstupu oproti referenčným súborom z Maestra.

---

## Hotové (presun sem po doladení)

| Dátum | Čo bolo doladené |
|-------|------------------|
| — | — |
