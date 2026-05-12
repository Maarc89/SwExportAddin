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

Los ficheros se guardan en subcarpetas en el mismo directorio que el/los drawing:
- `PDF`
- `DWG`

## Requisitos

- **Windows x64**.
- **SOLIDWORKS x64** (validado con **SOLIDWORKS 2025**).
- **.NET Framework 4.8** (target del proyecto).
- Visual Studio con soporte para proyectos **.NET Framework** (para compilar desde código fuente).
- Permisos de administrador para el registro COM.

## Build desde código fuente (Visual Studio)

1. Abre `SwExportAddin\SwExportAddin.csproj` en Visual Studio.
2. Selecciona:
   - **Configuration**: `Release`
   - **Platform**: `x64`
3. Compila el proyecto.
4. Salida esperada:
   - `SwExportAddin\bin\x64\Release\SwExportAddin.dll`

### Referencias de SOLIDWORKS

El proyecto usa interop desde rutas tipo:

- `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll`
- `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.swconst.dll`
- `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.swpublished.dll`

Si SOLIDWORKS está instalado en otra ruta/versión, ajusta los `HintPath` en `SwExportAddin.csproj`.

## Registro COM

El add-in debe registrarse para que SOLIDWORKS pueda cargarlo.

### Opción A: registro desde Visual Studio

- El proyecto ya tiene `RegisterForComInterop` habilitado para `x64`.
- Ejecuta Visual Studio como **Administrador** y compila en `Release|x64`.

### Opción B: registro manual (RegAsm)

Ejecuta como **Administrador**:

```powershell
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" "C:\ruta\SwExportAddin.dll" /codebase /tlb
```

Para desregistrar:

```powershell
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" "C:\ruta\SwExportAddin.dll" /unregister
```

## Instalación (usuario final)

1. Cierra SOLIDWORKS.
2. Ejecuta `SwExportAddin_Setup.exe` como **Administrador**.
3. Abre SOLIDWORKS.
4. Ve a **Tools > Add-ins**.
5. Activa **SwExportAddin** (Active Add-ins y opcionalmente Start Up).
6. Abre un drawing (`.slddrw`) y verifica la pestaña **Export Tools**.

## Uso

- **Exportar Plano** y **Exportar Carpeta** requieren drawing activo con ruta disponible.
- **Exportar Seleccionables** permite elegir archivos sin depender del documento activo.
