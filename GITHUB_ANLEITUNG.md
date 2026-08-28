# GitHub — Projekt herunterladen

Repository: **https://github.com/ferhatmertkaya/catch-if-you-can**

Branch mit allen Assets: **`cursor/integrate-external-assets-c0e7`**

---

## Schritt 1: Repo auf GitHub vorbereiten

- Repo **leer** lassen (kein README, keine .gitignore beim Erstellen)
- Oder: README ist ok — dann normal pushen

---

## Schritt 2: In Cursor (du hast GitHub schon verbunden ✓)

1. **Cursor → Settings → Cloud Agents** (oder Agents)
2. Repository hinzufügen: `ferhatmertkaya/catch-if-you-can`
3. In **diesem Agent-Chat** schreiben:  
   `Push das Projekt auf GitHub`

---

## Schritt 3: Auf dem Mac klonen

Wenn der Push erfolgreich war:

```bash
git clone https://github.com/ferhatmertkaya/catch-if-you-can.git
cd catch-if-you-can
git checkout cursor/integrate-external-assets-c0e7
```

Falls nur `main` existiert:

```bash
git clone https://github.com/ferhatmertkaya/catch-if-you-can.git
cd catch-if-you-can
```

---

## Schritt 4: Unity öffnen

1. Unity Hub → **Add** → Ordner `catch-if-you-can` (oder `CatchIfYouCan` darin)
2. Unity **6.3 LTS** + iOS Build Support
3. Menü: **Catch If You Can → Setup Project**
4. Szene `Assets/CatchIfYouCan/Scenes/03_Investigation.unity` → Play

---

## Manueller Push (falls du Zugriff auf den Agent-Ordner hast)

```bash
cd CatchIfYouCan
git add -A
git commit -m "Full asset integration"
git remote add origin https://github.com/ferhatmertkaya/catch-if-you-can.git
git push -u origin cursor/integrate-external-assets-c0e7
```

Bei leerem GitHub-Repo alternativ:

```bash
git push -u origin cursor/integrate-external-assets-c0e7:main
```
