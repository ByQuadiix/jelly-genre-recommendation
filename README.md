# Jellyfin Anime Recommendations Plugin

Ein intelligentes Jellyfin-Plugin (für Jellyfin 12.1+ / .NET 10), das auf der Startseite direkt unterhalb von „Weiterschauen“ eine neue Reihe mit wöchentlich wechselnden Anime-Empfehlungen hinzufügt.

Mit **Variante A** (Kompakte Reihe mit interaktiven Genre-Tabs) können Benutzer auf der Startseite bequem zwischen Genres wie *Action*, *Abenteuer*, *Comedy*, *Romance*, *Fantasy* und mehr wechseln – oder sich alle Empfehlungen auf einmal anzeigen lassen.

---

## ✨ Features

- 🎲 **Wöchentlich rotierende Zufallsauswahl:** Ausgewählte Titel bleiben eine ganze Woche lang stabil im Cache und rotieren automatisch wöchentlich.
- 🏷️ **Interaktive Genre-Tabs:** Schnelles Umschalten zwischen Genres direkt auf der Startseite ohne Neuladen der Seite.
- ⚙️ **Voll konfigurierbar über das Admin-Dashboard:**
  - Auswahl der Anime-Bibliothek über ein dynamisches Dropdown-Menü.
  - Frei anpassbare Liste von Genres (z. B. `Action, Abenteuer, Comedy, Romance, Fantasy, Sci-Fi`).
  - Einstellbare Anzahl von Titeln pro Genre.
  - **Checkbox „Bereits gesehene Anime ausschließen“:** Verhindert, dass Nutzer bereits gesehene Titel empfohlen bekommen.
  - **Button „Empfehlungen jetzt neu auswürfeln“:** Für sofortige Aktualisierung auf Knopfdruck ohne Wartezeit.
- 💉 **Automatische HTML-Injektion:** Erkennt das auf dem Server installierte **File Transformation Plugin** (`IAmParadox27/jellyfin-plugin-file-transformation`) und bindet das Client-Skript vollautomatisch in die WebUI ein.
- 🚀 **Vollautomatisierte GitHub CI/CD:**
  - Kompiliert das Plugin bei jedem neuen Release.
  - Erstellt die `Jellyfin.Plugin.AnimeRecommendations.zip`.
  - Berechnet die MD5-Prüfsumme.
  - Aktualisiert `manifest.json` automatisch im Repository.
  - Hängt das Zip-Archiv an das GitHub Release an.

---

## 📁 Projektstruktur

```
.
├── .github/
│   └── workflows/
│       └── release.yml              # GitHub Actions CI/CD Pipeline
├── src/
│   └── Jellyfin.Plugin.AnimeRecommendations/
│       ├── Api/
│       │   └── RecommendationsController.cs   # REST-API (/Recommendations/Weekly etc.)
│       ├── Configuration/
│       │   ├── PluginConfiguration.cs        # Einstellungen & Empfehlungs-Cache
│       │   └── configPage.html               # Admin-Dashboard WebUI
│       ├── Services/
│       │   ├── RecommendationService.cs       # Rotations- & Filter-Algorithmus
│       │   └── FileTransformationIntegration.cs # Anbindung an File Transformation
│       ├── Tasks/
│       │   └── WeeklyRecommendationTask.cs   # Wöchentlicher Hintergrund-Task
│       ├── Web/
│       │   └── recommendations.js            # Injiziertes Frontend-Skript
│       ├── Plugin.cs                         # Plugin-Einstiegspunkt
│       ├── PluginServiceRegistrator.cs       # Dependency Injection Registrierung
│       └── Jellyfin.Plugin.AnimeRecommendations.csproj
├── manifest.json                             # Jellyfin Repository Manifest
├── .gitignore
└── README.md
```

---

## 🚀 Erste Schritte: Auf GitHub hochladen & Release erstellen

1. **Repository initialisieren und zu GitHub pushen:**
   ```bash
   git init
   git add .
   git commit -m "feat: initial commit for Anime Recommendations plugin"
   git branch -M main
   git remote add origin https://github.com/<DEIN-BENUTZERNAME>/<DEIN-REPO-NAME>.git
   git push -u origin main
   ```

2. **Erstes Release auf GitHub erstellen:**
   - Gehe in deinem GitHub-Repository auf **Releases** -> **Draft a new release**.
   - Gib als Tag z. B. `v1.0.0.0` ein.
   - Klicke auf **Publish release**.
   - Die GitHub Action (`release.yml`) startet jetzt automatisch:
     1. Kompiliert den C#-Code für Jellyfin 12.1 (.NET 10).
     2. Packt die Zip-Datei.
     3. Berechnet die MD5-Prüfsumme.
     4. Schreibt die neue Version in die `manifest.json` und pusht sie in den `main`-Branch.
     5. Hängt die `Jellyfin.Plugin.AnimeRecommendations.zip` an das GitHub Release an.

---

## 📥 Installation auf deinem Jellyfin Server (Docker)

### Option 1: Über das Jellyfin-Repository (Empfohlen)

1. Öffne dein Jellyfin-Dashboard als Administrator.
2. Gehe zu **Dashboard** -> **Plugins** -> Reiter **Repositories**.
3. Klicke auf das **+** Symbol, um ein neues Repository hinzuzufügen:
   - **Name:** `Anime Recommendations Repo`
   - **Repository-URL:**  
     `https://raw.githubusercontent.com/<DEIN-BENUTZERNAME>/<DEIN-REPO-NAME>/main/manifest.json`
4. Klicke auf **Speichern**.
5. Wechsle auf den Reiter **Katalog**: Dort erscheint nun **Anime Recommendations**.
6. Klicke auf das Plugin und wähle **Installieren**.
7. Starte deinen Jellyfin Docker-Container einmal neu:
   ```bash
   docker restart <jellyfin-container-name>
   ```

---

### Option 2: Manuelle Installation

Falls du das Plugin direkt ohne Repository installieren möchtest:
1. Lade die `Jellyfin.Plugin.AnimeRecommendations.zip` aus deinen GitHub Releases herunter.
2. Entpacke den Inhalt in den Ordner `plugins/AnimeRecommendations` deines Jellyfin-Datenverzeichnisses (z. B. `/config/plugins/AnimeRecommendations`).
3. Starte den Docker-Container neu.

---

## ⚙️ Konfiguration

Nach der Installation findest du die Einstellungen unter:  
**Dashboard** -> **Plugins** -> **Anime Recommendations**

1. **Anime-Bibliothek:** Wähle deine Anime-Bibliothek aus der Liste aus.
2. **Empfohlene Genres:** Passe die Genres an (z. B. `Action, Abenteuer, Comedy, Romance, Fantasy, Sci-Fi`).
3. **Anzahl Empfehlungen pro Genre:** Standardmäßig 12 Titel.
4. **Bereits gesehene Anime ausschließen:** Aktiviere diese Checkbox, damit Nutzern keine Anime empfohlen werden, die sie bereits komplett gesehen haben.
5. **Jetzt neu auswürfeln:** Klicke auf diesen Button, um die Empfehlungen sofort testweise neu zu generieren.
6. Klicke auf **Speichern**.

---

## 🌐 Funktionsweise des File Transformation Plugins

Da auf deinem Server das Plugin **File Transformation** (`IAmParadox27/jellyfin-plugin-file-transformation`) installiert ist, registriert sich das *Anime Recommendations Plugin* beim Serverstart automatisch über Reflection und injiziert folgenden Tag dynamisch in die `index.html`:
```html
<script src="/Recommendations/ClientScript.js" defer></script>
```
Es sind **keine manuellen Änderungen** an den Webdateien des Docker-Containers notwendig! Das Skript bleibt auch nach Updates des Jellyfin-Containers erhalten.
