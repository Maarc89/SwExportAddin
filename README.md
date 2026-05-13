# SwExportAddin

Add-in de SOLIDWORKS para exportar archivos `.slddrw` a **PDF** y **DWG**.

## ¿Qué hace?

Añade una pestaña **Export Tools** con 3 comandos:

1. **Exportar Plano**
   - Exporta el documento activo (`.slddrw`) a PDF/DWG.
2. **Exportar Carpeta Completa**
   - Exporta los `.slddrw` de la carpeta del documento activo.
3. **Exportar Seleccionables**
   - Permite seleccionar uno o varios `.slddrw` para exportar.

Los ficheros se guardan en subcarpetas en el mismo directorio origen:
- `PDF`
- `DWG`

## Requisitos

- **Windows x64**.
- **SOLIDWORKS x64** (validado con **SOLIDWORKS 2025**).
- **.NET Framework 4.8** (target del proyecto).
- Visual Studio con soporte para proyectos **.NET Framework**.
- Permisos de administrador para registro COM.

## Build desde código fuente (Visual Studio)

1. Abre `SwExportAddin\SwExportAddin.csproj`.
2. Selecciona:
   - **Configuration**: `Release`
   - **Platform**: `x64`
3. Compila el proyecto.
4. Salida esperada:
   - `SwExportAddin\bin\x64\Release\SwExportAddin.dll`

### Referencias de SOLIDWORKS (portables)

El proyecto ya no depende de un `HintPath` absoluto fijo. Usa esta prioridad para resolver interop:

1. Variable de entorno `SOLIDWORKS_API_REDIST`
2. `$(ProgramFiles)\SOLIDWORKS Corp\SOLIDWORKS\api\redist`
3. `$(ProgramW6432)\SOLIDWORKS Corp\SOLIDWORKS\api\redist`

Si necesitas forzar ruta en una máquina concreta, define:

```powershell
$env:SOLIDWORKS_API_REDIST = "C:\Ruta\a\SOLIDWORKS\api\redist"
```

## Arquitectura actual del proyecto

- `SwAddin.cs`: integración COM/registro de comandos SOLIDWORKS.
- `ExportService.cs`: orquestador de exportaciones.
- `BatchExportHandler.cs`: flujo de exportación del documento activo.
- `FolderExportHandler.cs`: flujo de exportación por carpeta.
- `SelectExportHandler.cs`: flujo de exportación por selección manual.
- `ExportFileProcessor.cs`: lógica común de exportación por archivo.
- `ExportDialogService.cs`: diálogo de selección PDF/DWG.
- `ExportOptionsDialog.cs`: formulario WinForms del diálogo.
- `Logger.cs`: escritura de logs.
- `IconManager.cs`: gestión y renderizado de iconos embebidos.

## Registro COM

### Opción A: desde Visual Studio

- `RegisterForComInterop` está habilitado en `x64`.
- Ejecuta Visual Studio como **Administrador** y compila en `Release|x64`.

### Opción B: manual (RegAsm)

Ejecuta como **Administrador**:

```powershell
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" "C:\ruta\SwExportAddin.dll" /codebase /tlb
```

Para desregistrar:

```powershell
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" "C:\ruta\SwExportAddin.dll" /unregister
```

## Logs

Se guardan en:

`%LOCALAPPDATA%\SwExportAddin\Logs\SwExportAddin-.log`

El logging usa Serilog con niveles (`Information`, `Warning`, `Error`, `Debug`), rotación diaria y salida adicional a la ventana de depuración en desarrollo.

## Instalación

El instalador copia `SwExportAddin.dll` y las DLL dependientes necesarias de Serilog junto al add-in para que SOLIDWORKS pueda cargarlo correctamente en otro equipo.

## Instalación (usuario final)

1. Cierra SOLIDWORKS.
2. Ejecuta `SwExportAddin_Setup.exe` como **Administrador**.
3. Abre SOLIDWORKS.
4. Ve a **Tools > Add-ins**.
5. Activa **SwExportAddin**.
6. Abre un `.slddrw` y verifica la pestaña **Export Tools**.
