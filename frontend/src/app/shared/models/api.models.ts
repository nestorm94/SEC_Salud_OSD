/** Perfil del usuario autenticado devuelto por la API de autenticación del OSD. */
export interface UsuarioSesion {
  id: number;
  nombre: string;
  email?: string;
  dependencia_id?: number;
  dependencia?: string;
  linea_tematica_id?: number;
  linea_tematica?: string;
  roles: string[];
}

/** Respuesta exitosa del endpoint de login o refresh de token. */
export interface LoginResponse {
  token: string;
  usuario: UsuarioSesion;
}

/** Credenciales enviadas al formulario de inicio de sesión. */
export interface LoginRequest {
  usuario: string;
  password: string;
}

/** Métricas agregadas mostradas en el panel principal del dashboard. */
export interface DashboardResumen {
  total_archivos: number;
  cargas_pendientes: number;
  cargas_con_error: number;
  cargas_aprobadas: number;
  ultimos_cargues: UltimoCargue[];
}

/** Registro resumido de un cargue o prevalidación reciente. */
export interface UltimoCargue {
  id: number;
  origen?: string;
  dependencia: string;
  estado: string;
  archivo: string;
  fecha: string;
  usuario?: string;
}

/** Línea temática de indicadores de salud (ej. mortalidad, morbilidad). */
export interface LineaTematicaItem {
  id: number;
  codigo: string;
  nombre: string;
  descripcion?: string;
  activo?: boolean;
}

/** Indicador asociado a una línea temática para carga de archivos OSC. */
export interface IndicadorItem {
  id: number;
  linea_tematica_id: number;
  linea_tematica: string;
  codigo: string;
  nombre: string;
  descripcion?: string;
  activo?: boolean;
}

/** Archivo Excel subido por una dependencia para prevalidación o cargue. */
export interface ArchivoItem {
  id: number;
  dependencia_id: number;
  dependencia?: string;
  linea_tematica_id?: number;
  linea_tematica?: string;
  indicador_id?: number;
  indicador?: string;
  nombre_original: string;
  tipo_mime: string;
  tamano_bytes: number;
  creado_en: string;
  subido_por: string;
  observaciones?: string;
  estado?: string;
  estado_etiqueta?: string;
  fecha_validacion?: string;
  fecha_envio?: string;
}

/** Resultado de la prevalidación estructural y de datos de un archivo OSC. */
export interface ValidacionArchivoResponse {
  archivo_id: number;
  valido: boolean;
  mensaje?: string;
  errores_diccionario?: string[];
  errores_data?: string[];
  observaciones?: string[];
  total_errores_diccionario?: number;
  total_errores_data?: number;
  geografia?: Record<string, unknown>;
}

/** Cargue masivo procesado en el backend tras aprobar un archivo. */
export interface CargaItem {
  id: number;
  dependencia_id: number;
  dependencia: string;
  estado: string;
  fecha_inicio: string;
  fecha_fin?: string;
  archivo: string;
  usuario: string;
  total_errores: number;
}

/** Error de validación en una fila/columna de un cargue. */
export interface CargaError {
  id: number;
  fila: number;
  columna: string;
  mensaje: string;
  tipo: string;
}

/** Detalle de errores de un cargue para el modal de validaciones. */
export interface CargaErroresResponse {
  carga_id: number;
  estado: string;
  errores: CargaError[];
}

/** Respuesta paginada genérica de vistas de proyección poblacional o ASIS. */
export interface ProyeccionResponse {
  clave: string;
  pagina: number;
  tamanoPagina: number;
  totalFilas: number;
  totalPaginas: number;
  columnas: string[];
  filas: Record<string, unknown>[];
}

/** Departamento del catálogo DANE. */
export interface DepartamentoDto {
  codigoDane: string;
  nombreDepartamento: string;
}

/** Municipio del catálogo DANE con regional de salud. */
export interface MunicipioDto {
  codigoDane: string;
  codigoDepartamento: string;
  nombreMunicipio: string;
  regional: string;
}

/** Elemento simple de catálogo con código y etiqueta visible. */
export interface CatalogoSimpleDto {
  codigo: string;
  nombre: string;
}

/** Fila del indicador de mortalidad por cáncer de próstata. */
export interface IndicadorProstataDto {
  codigoDane: string;
  territorio: string;
  codigoTerritorio: string;
  regional: string;
  anio: number | null;
  area: string;
  muertes: number | null;
  poblacion: number | null;
  coeficiente: number | null;
  tasa: number | null;
}

/** Entidad organizacional que reporta datos al observatorio. */
export interface Dependencia {
  id: number;
  codigo: string;
  nombre: string;
  activo: boolean;
}

/** Usuario del módulo de administración (CRUD completo). */
export interface UsuarioAdmin {
  id: number;
  nombre_usuario: string;
  email?: string;
  activo: boolean;
  dependencia_id?: number;
  dependencia?: string;
  roles: string[];
}

/** Rol de seguridad asignable a usuarios del OSD. */
export interface Rol {
  id: number;
  nombre: string;
  descripcion?: string;
}

/** Plantilla Excel OSC asociada a una dependencia. */
export interface Plantilla {
  id: number;
  codigo: string;
  nombre: string;
  descripcion?: string;
  dependencia_id?: number;
  dependencia?: string;
  activo: boolean;
  total_campos: number;
}

/** Definición de columna/campo dentro de una plantilla OSC. */
export interface CampoPlantilla {
  id: number;
  nombre_campo: string;
  tipo_dato: string;
  obligatorio: boolean;
  descripcion?: string;
  longitud?: number;
  formato?: string;
  valores_permitidos?: string;
  orden: number;
}

/** Parámetros de consulta para vistas de proyección poblacional paginadas. */
export interface PaginatedQuery {
  pagina?: number;
  tamanoPagina?: number;
  territorio?: string;
  regional?: string;
  area?: string;
  sexo?: string;
  ano?: number;
  codigoDepartamento?: string;
  codigoMunicipio?: string;
}

/** Parámetros de consulta para vistas ASIS (Fase 4 del observatorio). */
export interface AsisQuery {
  pagina?: number;
  tamanoPagina?: number;
  vigencia?: number;
  codigoMunicipio?: string;
  nivelTerritorio?: 'DEPARTAMENTO' | 'MUNICIPIO';
  idProyeccionDane?: number;
}

/** Proyección DANE disponible para filtros de población ASIS. */
export interface AsisProyeccionDto {
  id: number;
  nombre: string;
  anioPublicacion?: number;
}

/** Metadatos de configuración de las vistas ASIS expuestas por la API. */
export interface AsisVistasMeta {
  capaPoblacion: 'legacy' | 'fact';
  idProyeccionDaneDefault: number;
}
