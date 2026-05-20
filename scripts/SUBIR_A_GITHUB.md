# Subir el proyecto a GitHub

Repositorio destino: **https://github.com/nestorm94/SEC_Salud_OSD**

## 1. Instalar Git (si no lo tiene)

Descargue e instale: https://git-scm.com/download/win  
Marque la opción **“Git from the command line”** en el instalador.

## 2. Ejecutar el script (desde la raíz del proyecto)

```powershell
cd C:\Users\Asus\Projects\Observatorios_Salud_Departamental_Cas
.\scripts\subir-github.ps1
```

El script:

- Inicializa Git (si hace falta)
- Configura `origin` → `https://github.com/nestorm94/SEC_Salud_OSD.git`
- Hace commit de todo el código (sin `node_modules`, `bin`, `uploads`, etc.)
- Hace `git push -u origin main`

## Problema: el repo se ve vacío en GitHub

Eso pasa si **nunca se hizo `git push`** (solo commit local) o si falló la autenticación.

En este proyecto, la regla `data/` en `.gitignore` **no debe ignorar** `backend/Observatorios.Api/Data/` (ya corregido a `/data/`).

## 3. Autenticación en GitHub

Si pide usuario y contraseña:

- **Usuario:** su nombre de usuario de GitHub (`nestorm94`)
- **Contraseña:** un **Personal Access Token** (no la contraseña de la cuenta)

Crear token: GitHub → **Settings** → **Developer settings** → **Personal access tokens** → **Tokens (classic)** → Generate new token → marque `repo` → copie el token.

## 4. Comandos manuales (alternativa)

```powershell
cd C:\Users\Asus\Projects\Observatorios_Salud_Departamental_Cas
git init
git branch -M main
git remote add origin https://github.com/nestorm94/SEC_Salud_OSD.git
git add -A
git commit -m "Publicación inicial Observatorio Salud Casanare"
git push -u origin main
```

## Qué no se sube (`.gitignore`)

- `node_modules/`, `frontend/dist/`
- `**/bin/`, `**/obj/`
- `uploads/`, `.env`, logs
