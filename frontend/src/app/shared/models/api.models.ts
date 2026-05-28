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

export interface LoginResponse {
  token: string;
  usuario: UsuarioSesion;
}

export interface LoginRequest {
  usuario: string;
  password: string;
}

export interface DashboardResumen {
  total_archivos: number;
  cargas_pendientes: number;
  cargas_con_error: number;
  cargas_aprobadas: number;
  ultimos_cargues: UltimoCargue[];
}

export interface UltimoCargue {
  id: number;
  origen?: string;
  dependencia: string;
  estado: string;
  archivo: string;
  fecha: string;
  usuario?: string;
}

export interface LineaTematicaItem {
  id: number;
  codigo: string;
  nombre: string;
  descripcion?: string;
  activo?: boolean;
}

export interface IndicadorItem {
  id: number;
  linea_tematica_id: number;
  linea_tematica: string;
  codigo: string;
  nombre: string;
  descripcion?: string;
  activo?: boolean;
}

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

export interface CargaError {
  id: number;
  fila: number;
  columna: string;
  mensaje: string;
  tipo: string;
}

export interface CargaErroresResponse {
  carga_id: number;
  estado: string;
  errores: CargaError[];
}

export interface ProyeccionResponse {
  clave: string;
  pagina: number;
  tamanoPagina: number;
  totalFilas: number;
  totalPaginas: number;
  columnas: string[];
  filas: Record<string, unknown>[];
}

export interface DepartamentoDto {
  codigoDane: string;
  nombreDepartamento: string;
}

export interface MunicipioDto {
  codigoDane: string;
  codigoDepartamento: string;
  nombreMunicipio: string;
  regional: string;
}

export interface CatalogoSimpleDto {
  codigo: string;
  nombre: string;
}

export interface Dependencia {
  id: number;
  codigo: string;
  nombre: string;
  activo: boolean;
}

export interface UsuarioAdmin {
  id: number;
  nombre_usuario: string;
  email?: string;
  activo: boolean;
  dependencia_id?: number;
  dependencia?: string;
  roles: string[];
}

export interface Rol {
  id: number;
  nombre: string;
  descripcion?: string;
}

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
