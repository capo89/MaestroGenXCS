# Korpus skrinky – režimy Bok vložený / Bok nalozený

Referenčný diel v MaestroGenXcs ostáva **Bok L** (prípadne Bok P). Rozdiel je v tom, **ako sú osadené dno a vrch** voči bokom a **ktoré plochy** sa vŕtajú pre kolíky.

Vzory sú v exportovanom XML z XCam (žiadny import do aplikácie – len referencia).

## Priečinky vzorov

| Priečinok | Význam skladby | Súbory dna/vrchu | Súbory bokov |
|-----------|----------------|------------------|--------------|
| `Korpus_Skrinka_Bok_nalozeny` | Boky **nalozené** (celá výška), dno/vrch **vložené** medzi bokmi | `Dno-vlozene.xml`, `Vrch-vlozene.xml` | `Bok L-nalozeny.xml`, `Bok P-nalozeny.xml` |
| `Korpus_Skrinka_Bok_vlozeny` | Boky **vložené** (medzi dnom a vrchom), dno/vrch **nalozené** cez boky | `Dno-nalozene.xml`, `Vrch-nalozene.xml` | `Bok L-vlozeny.xml`, `Bok P-vlozeny.xml` |

Prípona v názve súboru = **fyzické osadenie daného dielca** v korpuse, nie priamo text v `ConnectionMap`.

## Dva XCam projekty (šablóny)

- **1031021564** – panel dno/vrch (`dx1` × `dy1`, typicky **600 × 500** v ukážke)
- **1030998443** – bok (`dx1` výška – **~564** alebo **~1600** podľa korpusu)

## Vrtanie a mapovanie na `ConnectionMap`

### Korpus **Bok vložený** (`AssemblyCorpusMode.BokVlozeny`)

- **Dno/Vrch:** plocha **Top**, body cca (9, 30) a (591, 30), 3× roztec **128 mm**, hĺbka **13 mm**, refpos `dx1+dy1+dz1` (2)
- **Bok:** kolíky na **Top** po výške (3 rady × 128), partner dno/vrch
- **Spoj v MaestroGenXcs:** pravidlá „vložené“ – napr. Bok L **Right** ref 0 ↔ Dno **Top** ref 0
- **3D:** špeciálna orientácia panelu medzi bokmi (`BuildVlozenyPlacementTransform`)

### Korpus **Bok nalozený** (`AssemblyCorpusMode.BokNalozeny`)

- **Dno/Vrch:** vrtanie do **hrany** (Left/Right), `Y = dz1/2`, 3 stĺpce × 128, hĺbka **23 mm**
- **Bok:** kolíky na **Top** po šírke (3 stĺpce × 128), `pocetKolikovDno` / `Vrch`
- **Spoj v MaestroGenXcs:** pravidlá „naložené“ – Bok L **Top** ref 2 ↔ Dno **Left** ref 2
- **3D:** bežné umiestnenie – kontaktná plocha dna **Left** (pri ref. Bok L) k **Top** boku

## Typické rozmery (demo v aplikácii)

| Režim | Bok Dx (výška) | Dno/Vrch Dx (šírka) | Vzťah |
|-------|----------------|---------------------|--------|
| Bok vložený | 564 | 600 | 564 + 18 + 18 ≈ 600 |
| Bok nalozený | 1600 | 600 | bok na celú výšku skrinky, dno medzi bokmi |

Hrúbka **18 mm**, hĺbka korpusu v deme **320 mm** (`dy`).

## Spoločné parametre kolíkov

- Priemer **8 mm**
- Počet **3**, roztec **128 mm**
- Odsadenie od hrany cca **30 mm** (podľa plochy a smeru patternu)

## Polica / priečka

V oboch režimoch ostáva **Top** referenčného boku + **Left/Right** police (pravidlá „naložené“ / pevná polica v `ConnectionMap`).

## Aplikácia MaestroGenXcs

- Režim sa nastavuje **na zostavu** (ComboBox „Režim korpusu“).
- Demo načíta zostavy **V** (bok vložený) a **N** (bok nalozený).
- Propagácia kolíkov používa len spoje platné pre zvolený režim.
