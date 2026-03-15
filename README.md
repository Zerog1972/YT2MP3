# YT2MP3 — Convertisseur YouTube vers MP3

Application Windows WPF permettant de convertir des vidéos YouTube en fichiers MP3 à partir de leurs URLs.

## Fonctionnalités

- Collage de plusieurs URLs YouTube (une par ligne)
- Téléchargement automatique de `yt-dlp` et `ffmpeg` au premier lancement
- Conversion en MP3 qualité maximale (`--audio-quality 0`)
- Suppression automatique des URLs converties avec succès
- Dossier de sortie mémorisé entre les sessions
- Annulation à tout moment avec la touche `Échap`
- Barre de progression et statut en temps réel
- Journalisation des erreurs dans un fichier local

## Prérequis

- Windows 10 ou 11
- .NET Framework 4.8
- Connexion internet requise au premier lancement (pour télécharger les outils)

Les exécutables `yt-dlp.exe` et `ffmpeg.exe` sont téléchargés automatiquement dans le sous-dossier `tools\` à côté de l’exécutable. Aucune installation manuelle n’est nécessaire.

## Utilisation

1. Lancer `YT2MP3.exe`
2. Au premier lancement, patienter pendant le téléchargement automatique des outils
3. Coller une ou plusieurs URLs YouTube dans la zone de texte (une par ligne)
4. Choisir le dossier de destination (mémorisé automatiquement)
5. Cliquer sur `Convertir`
6. Annuler à tout moment avec `Échap`

Les fichiers MP3 sont nommés d’après le titre de la vidéo.

## Journalisation

- Fichier de log : `%LocalAppData%\YT2MP3\logs\app.log`
- Le log contient notamment les erreurs d’initialisation et les échecs de conversion.

## Structure du projet

```text
YT2MP3/
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── ToolManager.cs
├── Properties/
│   ├── Settings.settings
│   └── Settings.Designer.cs
└── tools/                 # Généré à l'exécution
    ├── yt-dlp.exe
    └── ffmpeg.exe
```

## Sources des outils téléchargés

- `yt-dlp` : https://github.com/yt-dlp/yt-dlp/releases/latest
- `ffmpeg` : https://github.com/yt-dlp/FFmpeg-Builds/releases/latest

## Compilation

Ouvrir `YT2MP3.sln` dans Visual Studio 2019 ou supérieur et lancer la compilation (`Ctrl+Shift+B`).

## Auteur

Thierry JOUVE — mars 2026
