# YT2MP3 — Convertisseur YouTube → MP3

Application Windows permettant de convertir des vidéos YouTube en fichiers MP3 à partir de leurs URLs.

---

## Fonctionnalités

- Collage de plusieurs URLs YouTube (une par ligne)
- Téléchargement automatique de **yt-dlp** et **ffmpeg** au premier lancement
- Conversion en MP3 qualité maximale (VBR 0)
- Suppression automatique des URLs converties avec succès
- Dossier de sortie mémorisé entre les sessions
- Annulation à tout moment via le bouton **Annuler** ou la touche **Échap**
- Barre de progression et statut en temps réel

---

## Prérequis

| Élément | Version minimale |
|---|---|
| Windows | 10 / 11 |
| .NET Framework | 4.8 |
| Connexion internet | Requise uniquement au premier lancement |

> **yt-dlp** et **ffmpeg** sont téléchargés automatiquement dans le sous-dossier `tools\` à côté de l'exécutable. Aucune installation manuelle n'est nécessaire.

---

## Utilisation

1. Lancer `YT2MP3.exe`
2. Au premier lancement, patienter pendant le téléchargement automatique des outils
3. Coller une ou plusieurs URLs YouTube dans la zone de texte (une par ligne)
4. Choisir le dossier de destination (mémorisé automatiquement)
5. Cliquer sur **▶ Convertir** ou appuyer sur **Entrée**
6. Annuler à tout moment avec **Échap**

Les fichiers MP3 sont nommés d'après le titre de la vidéo.

---

## Structure du projet

```
YT2MP3/
├── FormMain.cs            # Formulaire principal (UI + logique de conversion)
├── FormMain.Designer.cs   # Mise en page générée
├── ToolManager.cs         # Téléchargement et gestion de yt-dlp et ffmpeg
├── Program.cs             # Point d'entrée
├── Properties/
│   ├── Settings.settings  # Paramètre utilisateur : dossier de sortie
│   └── Settings.Designer.cs
└── tools/                 # Généré à l'exécution
    ├── yt-dlp.exe
    └── ffmpeg.exe
```

---

## Sources des outils téléchargés

| Outil | Source |
|---|---|
| yt-dlp | https://github.com/yt-dlp/yt-dlp/releases/latest |
| ffmpeg | https://github.com/yt-dlp/FFmpeg-Builds/releases/latest |

---

## Compilation

Ouvrir `YT2MP3.sln` dans Visual Studio 2019 ou supérieur et lancer la compilation (`Ctrl+Shift+B`).

---

## Auteur

Thierry JOUVE - 12 mars 2026
