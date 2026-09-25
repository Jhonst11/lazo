# Lazo — memoria del proyecto

Proyecto local de SARCA para transferir archivos entre equipos Windows 10/11 de una misma subred.

- Carpeta: `C:\Users\JQUIN\OneDrive\Cowork\05 Development\Lazo`.
- Interfaz: WPF en escala de grises, barra lateral oscura, superficie clara y tipografía monoespaciada. Referencia visual: instalador del plugin de Revit en `C:\Users\JQUIN\OneDrive\Documents\ARCHIVO-JA\MyPyRevitExtension\Extension.extension\pyJhonAndrew.tab\Instalador\setup-preview.png`; solo lectura.
- Activación: doble toque de Alt izquierdo; alternativa `Ctrl+Alt+L`; icono de bandeja.
- Red: descubrimiento UDP `48351`, transferencia TCP `48352`, misma subred IPv4 privada. Recepción voluntaria en `Descargas\Lazo` y verificación SHA-256.
- Rama inicial: `codex/initial-app`. Build: `scripts/build.ps1`; prueba: `scripts/test-network.ps1`; paquete: `scripts/package.ps1`.
- Estado: primera versión funcional local. Falta validación entre equipos físicos y cifrado/autenticación antes de uso en redes no confiables.
