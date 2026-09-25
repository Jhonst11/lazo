# Lazo — memoria del proyecto

Proyecto local de SARCA para transferir archivos entre equipos Windows 10/11 de una misma subred.

- Carpeta: `C:\Users\JQUIN\OneDrive\Cowork\05 Development\Lazo`.
- Interfaz: WPF en escala de grises. Tema Raycast oscuro y compacto, tema Vidrio translúcido inspirado en Liquid Glass; elección persistente. La versión inicial tomó como referencia visual el instalador del plugin de Revit en `C:\Users\JQUIN\OneDrive\Documents\ARCHIVO-JA\MyPyRevitExtension\Extension.extension\pyJhonAndrew.tab\Instalador\setup-preview.png`; solo lectura.
- Activación: doble toque de Alt izquierdo; alternativa `Ctrl+Alt+L`; icono de bandeja. La ventana principal y la alerta entrante se centran en el monitor del cursor al abrirse.
- Red: descubrimiento UDP `48351`, transferencia TCP `48352`, misma subred IPv4 privada. Recepción voluntaria en `Descargas\Lazo` y verificación SHA-256.
- Rama inicial: `codex/initial-app`. Build: `scripts/build.ps1`; pruebas: `scripts/test-network.ps1`, `scripts/test-installer.ps1`; instalador: `scripts/build-installer.ps1`; portable: `scripts/package.ps1`.
- Versión 0.2.0: instalador autónomo `dist/Lazo-Setup-0.2.0.exe`, registro de desinstalación y firewall privado/local. Sin firma digital.
- Estado: falta validar instalación y transferencia entre equipos físicos, además de cifrado/autenticación antes de uso en redes no confiables.
