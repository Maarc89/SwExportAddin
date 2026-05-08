# SwExportAddin

Add-in de SOLIDWORKS para exportar drawings (`.slddrw`) a **PDF** y **DWG**.

## ¿Qué hace?

Añade una pestaña **Export Tools** con 3 comandos:

1. **Export Batch**
   - Exporta el drawing activo a PDF y DWG.
2. **Export Folder**
   - Exporta todos los `.slddrw` de la carpeta del drawing activo.
3. **Export Select**
   - Permite seleccionar uno o varios `.slddrw` para exportar.

Los ficheros se guardan en subcarpetas:
- `PDF`
- `DWG`

## Requisitos

- SOLIDWORKS x64 (validado con SOLIDWORKS 2025)
- .NET Framework 4.8 o superior (4.8.1 también válido)
- Windows x64
- Permisos de administrador para instalar

## Instalación (usuario final)

1. Cierra SOLIDWORKS.
2. Ejecuta `SwExportAddin_Setup.exe` como **Administrador**.
3. Abre SOLIDWORKS.
4. Ve a **Tools > Add-ins**.
5. Activa **SwExportAddin** (Active Add-ins y opcionalmente Start Up).
6. Abre un drawing (`.slddrw`) y verifica la pestaña **Export Tools**.

## Uso

- **Export Batch** y **Export Folder** requieren drawing activo con ruta disponible.
- **Export Select** permite elegir archivos sin depender del documento activo.

## Licencia

Pendiente de definir (por ejemplo MIT).