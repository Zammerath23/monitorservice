# MonitorService

Sistema híbrido **RSS + scraping** en .NET (C#) que detecta cambios en webs y publica avisos en un grupo de Telegram.

- **RSS**: detecta entradas nuevas por GUID/link.
- **HTML estático**: descarga la página y extrae campos por selector CSS.
- **Playwright**: para webs con precio/stock cargados por JavaScript.
- **Snapshots en SQLite**: cada cambio queda registrado con timestamp.
- **Notificación a Telegram y/o Discord**: vía bot+grupo (Telegram) y/o webhook de canal (Discord). Puedes activar uno, otro o ambos.

## Para el usuario final

1. Descomprime el zip.
2. Configura **al menos uno** de los dos canales de aviso:

   ### Opción A — Telegram (grupo)
   1. Habla con [@BotFather](https://t.me/BotFather) → `/newbot` → copia el token.
   2. Crea un grupo, añade tu bot, envía un mensaje al grupo.
   3. Ejecuta `.\MonitorService.exe --discover-chat` y copia el `chat_id` que imprima.
   4. Pega ambos en `appsettings.json`:
      ```json
      "Telegram": { "BotToken": "123456:ABC...", "ChatId": "-1001234567890" }
      ```

   ### Opción B — Discord (canal con webhook)
   1. En el canal de Discord → ⚙ Editar canal → Integraciones → Webhooks → Nuevo webhook → **Copiar URL**.
   2. Pega la URL en `appsettings.json`:
      ```json
      "Discord": { "WebhookUrl": "https://discord.com/api/webhooks/...", "Username": "MonitorService" }
      ```

   Puedes activar las dos a la vez: deja vacío el bloque que no quieras usar y el monitor lo ignora.

3. Edita `sources.json` con las webs / RSS a vigilar (ver formato abajo).
4. Doble-clic en `MonitorService.exe` y deja la ventana abierta.

### (Opcional) Que se inicie solo al encender el PC

Doble-clic en **`instalar-autoarranque.bat`**. Crea un acceso directo en la carpeta "Inicio" de tu usuario para que `MonitorService.exe` arranque automáticamente cada vez que inicies sesión en Windows, con la ventana minimizada en la barra de tareas. **No necesita permisos de administrador.**

Para deshacerlo: doble-clic en **`desinstalar-autoarranque.bat`**.

> Si el monitor se cuelga, basta con cerrar su ventana y volver a hacer doble-clic en `MonitorService.exe`. El autoarranque no instala un servicio de Windows ni nada parecido — solo un acceso directo. Si quieres una instalación más profesional (servicio Windows, sin ventana, reinicio automático), dímelo.

> La primera vez que arranque con una fuente de tipo `playwright`, descargará Chromium (~150 MB) en `<DataDirectory>\playwright-browsers`. Solo ocurre una vez.

## Dónde se generan los ficheros (BD, cache de Playwright, etc.)

Por defecto, todo lo "generado" (no editable por el usuario) vive en una carpeta **`DataDirectory`** controlada por `appsettings.json`:

```json
{
  "DataDirectory": "",                       // vacío => junto al .exe (default)
  "Database": { "Path": "monitor.db" }       // relativa => DataDirectory\monitor.db; absoluta => tal cual
}
```

| `DataDirectory` | `Database.Path` | Dónde acaba la BD |
| --- | --- | --- |
| `""` | `"monitor.db"` | `<exeDir>\monitor.db` |
| `"data"` | `"monitor.db"` | `<exeDir>\data\monitor.db` |
| `"C:\\Datos\\Monitor"` | `"monitor.db"` | `C:\Datos\Monitor\monitor.db` |
| cualquiera | `"D:\\backup\\m.db"` | `D:\backup\m.db` (absoluta gana) |

Lo mismo aplica al cache de Playwright: siempre `<DataDirectory>\playwright-browsers\`. La carpeta se crea sola al arrancar.

`sources.json` y `appsettings.json` se quedan **siempre junto al `.exe`** — son configuración del usuario, no datos generados.

Para inspeccionar las rutas resueltas:

```powershell
.\MonitorService.exe --paths
```

## Añadir fuentes con ayuda de una IA

Si no quieres trastear con selectores CSS a mano, hay un archivo **[AI-HELPER.md](AI-HELPER.md)** pensado para esto: lo copias, lo pegas en ChatGPT (o Claude, o Gemini), le dices qué quieres monitorizar, y te devuelve un bloque JSON listo para pegar dentro de `sources.json`. Las instrucciones de uso están al principio del propio archivo.

## Formato de `sources.json`

```json
{
  "defaultIntervalMinutes": 15,
  "sources": [
    {
      "name": "Phoronix RSS",
      "type": "rss",
      "url": "https://www.phoronix.com/rss.php",
      "intervalMinutes": 30,
      "enabled": true
    },
    {
      "name": "PcComponentes - RTX 5090",
      "type": "html",
      "url": "https://www.pccomponentes.com/zotac-gaming-rtx-5090",
      "intervalMinutes": 10,
      "watch": {
        "price": { "selector": "#pdp-price-current-integer", "attribute": "text" },
        "stock": { "selector": "[data-e2e='stock-status']",  "attribute": "text" }
      },
      "alertOn": ["price", "stock"]
    },
    {
      "name": "Amazon - SSD",
      "type": "playwright",
      "url": "https://www.amazon.es/dp/B0BHJJ9Y77",
      "intervalMinutes": 20,
      "waitFor": "#corePriceDisplay_desktop_feature_div",
      "watch": {
        "price": { "selector": ".a-price .a-offscreen", "attribute": "text" },
        "stock": { "selector": "#availability span",     "attribute": "text" }
      },
      "alertOn": ["price", "stock"]
    }
  ]
}
```

### Campos
| Campo | Tipo | Notas |
| --- | --- | --- |
| `type` | `rss` \| `html` \| `playwright` | RSS detecta entradas nuevas; HTML/Playwright extraen campos. |
| `intervalMinutes` | int | Opcional; si falta, usa `defaultIntervalMinutes`. |
| `watch` | dict | Diccionario `nombreCampo -> { selector, attribute }`. `attribute: "text"` por defecto, o `"html"`, o el nombre del atributo HTML (p. ej. `"href"`, `"content"`). |
| `alertOn` | string[] | Lista de campos que disparan notificación. Para RSS: **si lo omites, solo notifica items nuevos (NewItem)**, nunca cambios en campos de items existentes (evita spam por re-touches de pubDate). Para HTML/Playwright: si lo omites, notifica cualquier cambio. |
| `waitFor` | string (solo playwright) | Selector CSS que se espera antes de extraer. |
| `headers` | dict | Cabeceras HTTP extra (cookies de sesión, `Accept-Language`, etc.). |
| `enabled` | bool | Pausar sin borrar. |
| `seedSilently` | bool | Primera ejecución guarda snapshots pero no notifica. Recomendado para RSS con backlog. |
| `filter.titleMatches` | regex | Solo conserva items cuyo título encaje con el regex .NET. Útil para RSS de tienda generalista que solo te interesan ciertos productos. Soporta `(?i)` para case-insensitive. |
| `discordMention` | string | Texto prepended al mensaje de Discord para disparar notificación. Valores: `"@here"`, `"@everyone"`, `"<@USER_ID>"`, `"<@&ROLE_ID>"`, o combinaciones. Si lo omites, no se pinga a nadie. **Importante**: solo funciona en Discord (los embeds no disparan menciones, por eso lo metemos en `content`). |

## Para el desarrollador

```powershell
# Build local
dotnet build .\src\MonitorService

# Ejecutar en desarrollo
dotnet run --project .\src\MonitorService

# Generar distribuible (single .exe self-contained en .\dist)
.\publish.ps1
```

## Estructura

```
src/MonitorService/
├── Program.cs                   Composición DI + CLI
├── Worker.cs                    Loop por fuente con su intervalo
├── ChatDiscovery.cs             Modo --discover-chat
├── Configuration/               sources.json + appsettings.json
├── Monitors/                    RSS, HTML, Playwright
├── Detection/                   Cálculo de deltas
├── Persistence/                 SQLite (esquema autogenerado)
└── Notifications/               Console + Telegram
```
