# Colección Postman — Observatorio OSD

## Importar

1. Abrir Postman → **Import**.
2. Seleccionar:
   - `Observatorio-OSD.postman_collection.json`
   - `Observatorio-OSD.postman_environment.json`
3. Activar el entorno **Observatorio OSD — Local**.

## Uso

1. Ejecutar **Auth → Login** (guarda `token` automáticamente).
2. Ejecutar el resto de carpetas (usan `Bearer {{token}}`).

## Ambientes

| Entorno | `baseUrl` |
|---------|-----------|
| Desarrollo API | `http://localhost:5289` |
| IIS local | `http://localhost:8081` |
| Producción | `http://<servidor>:8081` |

## Notas

- Cambie `usuario` y `password` en el entorno para pruebas con otros roles.
- **Público → Próstata** no requiere token.
- Para IIS, API y frontend comparten el mismo origen; use la URL del sitio.
