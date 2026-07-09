using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

/// <summary>
/// Validación en dos niveles: hoja Diccionario_datos y hoja DATA (plantilla OSC V.2).
/// </summary>
public sealed class OscPlantillaValidacionService(IGeografiaValidacionService? geografia = null)
{
    private const string HojaDiccionarioNombre = "Diccionario_datos";

    private static readonly (string Canon, string Etiqueta)[] ColumnasRequeridasDiccionario =
    [
        ("id_row", "Id"),
        ("nombre_campo", "Nombre de la variable"),
        ("descripcion", "Descipción de la variable"),
        // ("llave_primaria", "Llave Primaria"),
        // ("llave_foranea", "Llave Foránea"),
        ("obligatorio", "Campo obligatorio"),
        ("id_variable", "Id. de la variable"),
        ("tipo_dato", "Tipo de datos"),
        ("longitud", "Longitud"),
        ("dominios", "Dominios (Categorías, valores)"),
        ("unidad_medida", "Unidad de medida"),
        ("campo_calculado", "Campo calculado"),
        ("formula", "Fórmula aplicada"),
    ];

    private static readonly (string Canon, string Etiqueta)[] CamposObligatoriosPorFilaDiccionario =
    [
        ("id_row", "Id"),
        ("nombre_campo", "Nombre de la variable"),
        ("descripcion", "Descipción de la variable"),
        // ("llave_primaria", "Llave Primaria"),
        // ("llave_foranea", "Llave Foránea"),
        ("obligatorio", "Campo obligatorio"),
        ("tipo_dato", "Tipo de datos"),
        ("longitud", "Longitud"),
        ("dominios", "Dominios (Categorías, valores)"),
        ("unidad_medida", "Unidad de medida"),
        ("campo_calculado", "Campo calculado"),
        ("formula", "Fórmula aplicada"),
    ];

    /// <summary>Ejecuta validación completa de hojas Diccionario_datos y DATA.</summary>
    public OscValidacionResult Validar(Stream excelStream)
    {
        var erroresDict = new List<string>();
        var erroresData = new List<string>();
        var observaciones = new List<string>();

        excelStream.Position = 0;
        using var wb = new XLWorkbook(excelStream);

        var hojaDict = BuscarHojaDiccionario(wb);
        if (hojaDict is null)
        {
            erroresDict.Add("Falta la hoja Diccionario_datos.");
            return Resultado(false, erroresDict, erroresData, observaciones, [], [], null);
        }

        var filaEnc = DiccionarioOscV2Reader.BuscarFilaEncabezados(hojaDict);
        if (filaEnc <= 0)
            filaEnc = DiccionarioOscV2Reader.FilaEncabezadosDefault;

        var headers = DiccionarioOscV2Reader.ResolverColumnas(hojaDict, filaEnc);
        ValidarColumnasDiccionario(headers, erroresDict);
        if (erroresDict.Count > 0)
            return Resultado(false, erroresDict, erroresData, observaciones, [], [], null);

        var definiciones = LeerDefinicionesDiccionario(hojaDict, filaEnc, headers, erroresDict);
        if (erroresDict.Count > 0)
            return Resultado(false, erroresDict, erroresData, observaciones, [], [], null);

        if (definiciones.Count == 0)
        {
            erroresDict.Add("La hoja Diccionario_datos no contiene registros de variables.");
            return Resultado(false, erroresDict, erroresData, observaciones, [], [], null);
        }

        var campos = definiciones.Select(d => d.Campo).ToList();
        var hojaData = BuscarHojaData(wb);
        if (hojaData is null)
        {
            erroresData.Add("Falta la hoja DATA.");
            return Resultado(false, erroresDict, erroresData, observaciones, campos, [], null);
        }
        var resumenGeo = geografia is null
            ? null
            : ValidarHojaData(hojaData, definiciones, erroresData, observaciones, geografia);
        var filas = LeerFilasData(hojaData, definiciones);
        var esValido = erroresDict.Count == 0 && erroresData.Count == 0;
        return Resultado(esValido, erroresDict, erroresData, observaciones, campos, filas, resumenGeo);
    }

    private static void ValidarColumnasDiccionario(
        IReadOnlyDictionary<string, int> headers,
        List<string> errores)
    {
        foreach (var (canon, etiqueta) in ColumnasRequeridasDiccionario)
        {
            if (!headers.ContainsKey(canon))
                errores.Add($"Falta la columna obligatoria «{etiqueta}» en la hoja {HojaDiccionarioNombre}.");
        }
    }

    private static List<DefinicionVariableOsc> LeerDefinicionesDiccionario(
        IXLWorksheet hoja,
        int filaEncabezado,
        IReadOnlyDictionary<string, int> headers,
        List<string> errores)
    {
        var list = new List<DefinicionVariableOsc>();
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string LeerCelda(int fila, string canon) =>
            headers.TryGetValue(canon, out var col) ? ObtenerTexto(hoja, fila, col) : "";

        var ultimaFilaConRegistro = ObtenerUltimaFilaRegistroDiccionario(hoja, filaEncabezado, LeerCelda);
        if (ultimaFilaConRegistro <= filaEncabezado)
            return list;

        for (var r = filaEncabezado + 1; r <= ultimaFilaConRegistro; r++)
        {
            var nombre = LeerCelda(r, "nombre_campo");
            if (string.IsNullOrWhiteSpace(nombre))
                continue;

            string Leer(string canon) => LeerCelda(r, canon);

            foreach (var (canon, etiqueta) in CamposObligatoriosPorFilaDiccionario)
            {
                if (string.IsNullOrWhiteSpace(Leer(canon)))
                    errores.Add($"En la hoja {HojaDiccionarioNombre}, fila {r}, columna {etiqueta} está vacía.");
            }

            if (!nombres.Add(nombre))
                errores.Add($"En la hoja {HojaDiccionarioNombre}, fila {r}: la variable «{nombre}» está duplicada.");

            var tipoRaw = Leer("tipo_dato");
            var tipoCanon = MapearTipoDatoCanon(tipoRaw, nombre);
            var requiereNumerico = EsCampoNumericoPorNombre(nombre)
                || EsTipoNumericoDiccionario(tipoRaw)
                || DebeValidarComoNumerico(tipoRaw, tipoCanon, nombre);
            if (requiereNumerico)
                tipoCanon = "entero";
            var tipoEtiqueta = EtiquetaTipo(tipoRaw, tipoCanon);

            var longStr = Leer("longitud");
            var longitud = InterpretarLongitudMaximaCaracteres(longStr, tipoCanon, nombre, requiereNumerico);
            if (!string.IsNullOrWhiteSpace(longStr) && longitud is null
                && !EsLongitudFormatoNumericoSql(longStr) && !int.TryParse(longStr.Trim(), out _))
            {
                errores.Add($"En la hoja {HojaDiccionarioNombre}, fila {r}, columna Longitud: valor «{longStr}» no válido.");
            }

            var dominiosRaw = Leer("dominios");
            string? dominioRegla = null;
            if (!string.IsNullOrWhiteSpace(dominiosRaw))
            {
                if (requiereNumerico)
                {
                    if (EsTextoReglaDominio(dominiosRaw) && !EsReglaTextoAlfanumerico(dominiosRaw.ToLowerInvariant()))
                        dominioRegla = dominiosRaw.Trim();
                }
                else if (tipoCanon == "texto" || EsReglaTextoAlfanumerico(dominiosRaw.ToLowerInvariant()))
                    dominioRegla = dominiosRaw.Trim();
                else if (EsTextoReglaDominio(dominiosRaw))
                    dominioRegla = dominiosRaw.Trim();
            }
            var dominios = dominioRegla is null
                ? InterpretarDominiosPermitidos(dominiosRaw, tipoCanon, nombre)
                : [];
            var obligatorioData = EsSi(Leer("obligatorio"));
            // Llave primaria / foránea: deshabilitado por ahora (no validar ni exigir en diccionario).
            // var llavePk = EsSi(Leer("llave_primaria"));
            const bool llavePk = false;
            var calculado = EsSi(Leer("campo_calculado"));

            var campo = new CampoDiccionarioDto(
                nombre,
                tipoCanon,
                obligatorioData,
                NullIfEmpty(Leer("descripcion")),
                longitud,
                NullIfEmpty(Leer("formula")),
                dominios.Count > 0 ? string.Join("; ", dominios) : null,
                null,
                null,
                list.Count);

            list.Add(new DefinicionVariableOsc(
                r,
                nombre,
                tipoRaw,
                tipoCanon,
                tipoEtiqueta,
                requiereNumerico,
                obligatorioData,
                llavePk,
                calculado,
                longitud,
                dominios,
                dominioRegla,
                campo));
        }

        return list;
    }

    /// <summary>Última fila con «Nombre de la variable» diligenciado (ignora filas vacías al final de la hoja).</summary>
    private static int ObtenerUltimaFilaRegistroDiccionario(
        IXLWorksheet hoja,
        int filaEncabezado,
        Func<int, string, string> leer)
    {
        var lastRow = hoja.LastRowUsed()?.RowNumber() ?? filaEncabezado;
        var ultima = filaEncabezado;
        for (var r = filaEncabezado + 1; r <= lastRow; r++)
        {
            if (!string.IsNullOrWhiteSpace(leer(r, "nombre_campo")))
                ultima = r;
        }
        return ultima;
    }

    private static GeografiaResumenDto? ValidarHojaData(
        IXLWorksheet hoja,
        IReadOnlyList<DefinicionVariableOsc> definiciones,
        List<string> errores,
        List<string> observaciones,
        IGeografiaValidacionService geo)
    {
        var range = hoja.RangeUsed();
        if (range is null)
        {
            errores.Add("La hoja DATA está vacía.");
            return null;
        }

        var filaHeader = BuscarFilaEncabezadosData(hoja, definiciones, range);
        var (headerExacto, headerNormalizado) = ConstruirMapasEncabezadosData(hoja, filaHeader, range);
        var mapaPorColumna = ConstruirMapaValidacionPorColumna(
            headerExacto, headerNormalizado, definiciones, errores);

        if (mapaPorColumna.Count == 0)
        {
            errores.Add("No se pudieron relacionar columnas de DATA con el Diccionario_datos. Revise que los encabezados coincidan con «Nombre de la variable».");
            return null;
        }

        var lastRow = range.LastRow().RowNumber();
        var filasDatos = new List<int>();
        for (var r = filaHeader + 1; r <= lastRow; r++)
        {
            if (!FilaVacia(hoja.Row(r))) filasDatos.Add(r);
        }

        if (filasDatos.Count == 0)
        {
            errores.Add("La hoja DATA no contiene filas de datos.");
            return null;
        }

        foreach (var fila in filasDatos)
        {
            foreach (var (col, def) in mapaPorColumna)
            {
                var valor = ObtenerTexto(hoja, fila, col);
                var vacio = string.IsNullOrWhiteSpace(valor);

                if (def.EsCalculado && vacio)
                {
                    observaciones.Add(
                        $"Fila {fila}, columna {def.NombreVariable}: campo calculado vacío (se acepta si la fórmula lo define).");
                    continue;
                }

                if (def.ObligatorioEnData && vacio)
                {
                    errores.Add($"Fila {fila}, columna {def.NombreVariable}: el campo es obligatorio.");
                    continue;
                }

                if (vacio) continue;

                if (!ValidarTipo(valor, def, out _))
                {
                    errores.Add(MensajeTipoInvalido(fila, def, valor));
                    continue;
                }

                if (def.LongitudMaxima.HasValue && valor.Length > def.LongitudMaxima.Value)
                {
                    var msgLong = def.RequiereNumerico
                        ? $"Fila {fila}, columna {def.NombreVariable}: el valor «{valor}» supera la longitud de {def.LongitudMaxima.Value} dígitos definida en el diccionario."
                        : $"Fila {fila}, columna {def.NombreVariable}: el valor «{valor}» tiene {valor.Length} caracteres; el máximo permitido es {def.LongitudMaxima.Value}.";
                    errores.Add(msgLong);
                }

                if (!string.IsNullOrWhiteSpace(def.DominioRegla))
                {
                    if (!CumpleReglaDominio(valor, def.DominioRegla, def.TipoCanon))
                    {
                        errores.Add(
                            $"Fila {fila}, columna {def.NombreVariable}: el valor «{valor}» no cumple la regla del diccionario ({def.DominioRegla}).");
                    }
                }
                else if (def.Dominios.Count > 0 && !ValorEnDominio(valor, def))
                {
                    errores.Add(
                        $"Fila {fila}, columna {def.NombreVariable}: el valor «{valor}» no está en el dominio permitido ({FormatearDominiosResumen(def.Dominios)}).");
                }
            }
        }

        // No validar duplicados solo en CODIGO DIVIPOLA (varias filas por municipio/sexo/año).
        // Duplicado = misma combinación divipola + año + sexo (si existen esas columnas).
        ValidarRegistrosDuplicadosPorClaveCompuesta(hoja, filasDatos, mapaPorColumna, errores);
        return ValidarGeografia(hoja, filaHeader, filasDatos, errores, observaciones, geo);
    }

    private static GeografiaResumenDto ValidarGeografia(
        IXLWorksheet hoja,
        int filaHeader,
        IReadOnlyList<int> filasDatos,
        List<string> errores,
        List<string> observaciones,
        IGeografiaValidacionService geo)
    {
        var range = hoja.RangeUsed()!;
        var (headerExacto, headerNormalizado) = ConstruirMapasEncabezadosData(hoja, filaHeader, range);
        int? Buscar(IEnumerable<string> nombres)
        {
            foreach (var n in nombres)
            {
                if (ResolverColumnaData(headerExacto, headerNormalizado, n, out var c))
                    return c;
            }
            return null;
        }

        var colMunNom = Buscar([
            "Municipio","Nombre municipio","Nombre del municipio","municipio_residencia","municipio_ocurrencia","municipio_atencion",
            "municipio_procedencia","municipio_notificacion","municipio_nacimiento"
        ]);
        var colMunCod = Buscar([
            "Código DANE","Codigo DANE","CODIGO_DANE","Código DIVIPOLA","Codigo DIVIPOLA","CODIGO_DIVIPOLA",
            "cod_municipio","codigo_municipio","COD_MPIO","COD_MUNICIPIO","DIVIPOLA_MUNICIPIO"
        ]);
        var colDepNom = Buscar([
            "Departamento","Nombre departamento","Nombre del departamento","departamento_residencia","departamento_ocurrencia",
            "departamento_atencion","departamento_procedencia","departamento_notificacion","departamento_nacimiento"
        ]);
        var colDepCod = Buscar([
            "Código departamento","Codigo departamento","CODIGO_DEPARTAMENTO","Código DANE departamento","Codigo DANE departamento",
            "COD_DEPTO","COD_DANE_DEPTO","DIVIPOLA_DEPARTAMENTO"
        ]);

        if (colMunNom is null && colMunCod is null && colDepNom is null && colDepCod is null)
        {
            var obs = "No se detectaron columnas geográficas para validar.";
            observaciones.Add(obs);
            return new GeografiaResumenDto(0, 0, 0, 0, 0, 0, 0, obs);
        }

        var cat = geo.ObtenerContexto();
        var total = 0;
        var codMunBad = 0;
        var munBad = 0;
        var codDepBad = 0;
        var depBad = 0;
        var codMunIncons = 0;
        var depMunIncons = 0;

        foreach (var fila in filasDatos)
        {
            total++;
            var vMunNom = colMunNom is int cmn ? ObtenerTexto(hoja, fila, cmn) : "";
            var vMunCod = colMunCod is int cmc ? ObtenerTexto(hoja, fila, cmc) : "";
            var vDepNom = colDepNom is int cdn ? ObtenerTexto(hoja, fila, cdn) : "";
            var vDepCod = colDepCod is int cdc ? ObtenerTexto(hoja, fila, cdc) : "";

            var munCod = NormalizarCodigoGeo(vMunCod, 5, out var munPadded);
            var depCod = NormalizarCodigoGeo(vDepCod, 2, out _);
            var munNomN = geo.NormalizarTexto(vMunNom);
            var depNomN = geo.NormalizarTexto(vDepNom);

            if (munPadded)
                observaciones.Add($"Fila {fila}, columna {NombreColumna(colMunCod)}: código municipio completado temporalmente con ceros a la izquierda para validar.");

            if (!string.IsNullOrWhiteSpace(munCod) && !geo.ValidarCodigoMunicipio(munCod))
            {
                codMunBad++;
                errores.Add($"Fila {fila}, columna {NombreColumna(colMunCod)}: el código DANE/DIVIPOLA no existe en dim_municipios.");
            }
            if (!string.IsNullOrWhiteSpace(vMunNom) && !geo.ValidarNombreMunicipio(vMunNom))
            {
                munBad++;
                errores.Add($"Fila {fila}, columna {NombreColumna(colMunNom)}: el municipio ‘{vMunNom}’ no existe en dim_municipios.");
            }
            if (!string.IsNullOrWhiteSpace(depCod) && !geo.ValidarCodigoDepartamento(depCod))
            {
                codDepBad++;
                errores.Add($"Fila {fila}, columna {NombreColumna(colDepCod)}: el código de departamento no existe en dim_departamento.");
            }
            if (!string.IsNullOrWhiteSpace(vDepNom) && !geo.ValidarNombreDepartamento(vDepNom))
            {
                depBad++;
                errores.Add($"Fila {fila}, columna {NombreColumna(colDepNom)}: el departamento ‘{vDepNom}’ no existe en dim_departamento.");
            }

            if (!string.IsNullOrWhiteSpace(munCod) && !string.IsNullOrWhiteSpace(vMunNom)
                && cat.MunicipiosPorCodigo.TryGetValue(munCod, out var mInfo)
                && !string.Equals(geo.NormalizarTexto(mInfo.NombreMunicipio), munNomN, StringComparison.OrdinalIgnoreCase))
            {
                codMunIncons++;
                errores.Add($"Fila {fila}: el código {munCod} no corresponde al municipio {vMunNom}. El municipio correcto es {mInfo.NombreMunicipio}.");
            }

            if (!string.IsNullOrWhiteSpace(depCod) && !string.IsNullOrWhiteSpace(munCod))
            {
                if (!geo.ValidarDepartamentoMunicipio(depCod, munCod))
                {
                    depMunIncons++;
                    errores.Add($"Fila {fila}: el municipio {vMunNom} no pertenece al departamento indicado.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(depCod) && !string.IsNullOrWhiteSpace(vMunNom))
            {
                var candidates = cat.MunicipiosPorNombreNormalizado.GetValueOrDefault(munNomN) ?? [];
                if (candidates.Count > 0 && candidates.All(c => !string.Equals(c.CodigoDepartamento, depCod, StringComparison.OrdinalIgnoreCase)))
                {
                    depMunIncons++;
                    errores.Add($"Fila {fila}: el municipio {vMunNom} no pertenece al departamento indicado.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(depNomN) && !string.IsNullOrWhiteSpace(vMunNom))
            {
                var depCode = cat.DepartamentosPorNombreNormalizado.GetValueOrDefault(depNomN) ?? "";
                if (!string.IsNullOrWhiteSpace(depCode))
                {
                    var candidates = cat.MunicipiosPorNombreNormalizado.GetValueOrDefault(munNomN) ?? [];
                    if (candidates.Count > 0 && candidates.All(c => !string.Equals(c.CodigoDepartamento, depCode, StringComparison.OrdinalIgnoreCase)))
                    {
                        depMunIncons++;
                        errores.Add($"Fila {fila}: el municipio {vMunNom} no pertenece al departamento indicado.");
                    }
                }
            }
        }

        return new GeografiaResumenDto(total, codMunBad, munBad, codDepBad, depBad, codMunIncons, depMunIncons, null);

        string NombreColumna(int? col)
        {
            if (col is null) return "Geografía";
            var txt = ObtenerTexto(hoja, filaHeader, col.Value);
            return string.IsNullOrWhiteSpace(txt) ? "Geografía" : txt;
        }
    }

    private static string NormalizarCodigoGeo(string? input, int width, out bool padded)
    {
        padded = false;
        if (string.IsNullOrWhiteSpace(input)) return "";
        var v = input.Trim();
        if (v.Contains('.')) v = v.Split('.')[0];
        v = Regex.Replace(v, @"\s+", "");
        if (!Regex.IsMatch(v, @"^\d+$")) return v;
        if (v.Length < width)
        {
            padded = true;
            return v.PadLeft(width, '0');
        }
        return v;
    }

    /// <summary>
    /// Detecta filas repetidas por clave compuesta (código DIVIPOLA + año + sexo).
    /// El mismo código DIVIPOLA en varias filas (Total / Hombres / Mujeres) es válido.
    /// </summary>
    private static void ValidarRegistrosDuplicadosPorClaveCompuesta(
        IXLWorksheet hoja,
        IReadOnlyList<int> filasDatos,
        IReadOnlyDictionary<int, DefinicionVariableOsc> mapaPorColumna,
        List<string> errores)
    {
        var porColumna = mapaPorColumna
            .GroupBy(kv => kv.Value.NombreVariable, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.OrdinalIgnoreCase);

        int? ColDe(params string[] tokens)
        {
            foreach (var kv in porColumna)
            {
                var n = DiccionarioOscV2Reader.Normalizar(kv.Key);
                if (tokens.Any(t => n.Contains(t, StringComparison.Ordinal)))
                    return kv.Value;
            }
            return null;
        }

        var colDivipola = ColDe("divipola");
        var colAnio = ColDe("ano", "anio");
        var colSexo = ColDe("sexo");

        if (colDivipola is null) return;

        var claves = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var fila in filasDatos)
        {
            var codigo = ObtenerTexto(hoja, fila, colDivipola.Value).Trim();
            if (string.IsNullOrWhiteSpace(codigo)) continue;

            var partes = new List<string> { $"d:{codigo}" };
            if (colAnio is int ca)
            {
                var anio = ObtenerTexto(hoja, fila, ca).Trim();
                if (!string.IsNullOrWhiteSpace(anio)) partes.Add($"a:{anio}");
            }
            if (colSexo is int cs)
            {
                var sexo = ObtenerTexto(hoja, fila, cs).Trim();
                if (!string.IsNullOrWhiteSpace(sexo)) partes.Add($"s:{sexo}");
            }

            var clave = string.Join("|", partes);
            if (!claves.TryGetValue(clave, out var filas))
            {
                filas = [];
                claves[clave] = filas;
            }
            filas.Add(fila);
        }

        foreach (var kv in claves.Where(c => c.Value.Count > 1))
        {
            var filas = string.Join(", ", kv.Value.OrderBy(f => f));
            var codigo = kv.Key.Split('|')[0];
            if (codigo.StartsWith("d:", StringComparison.OrdinalIgnoreCase))
                codigo = codigo[2..];
            errores.Add(
                $"Registro duplicado: el mismo código DIVIPOLA «{codigo}» con los mismos datos de año/sexo aparece en las filas {filas}. Revise que cada fila sea única.");
        }
    }

    private static List<DatosFilaDto> LeerFilasData(
        IXLWorksheet hoja,
        IReadOnlyList<DefinicionVariableOsc> definiciones)
    {
        var filas = new List<DatosFilaDto>();
        var range = hoja.RangeUsed();
        if (range is null) return filas;

        var filaHeader = range.FirstRow().RowNumber();
        var (headerExacto, headerNormalizado) = ConstruirMapasEncabezadosData(hoja, filaHeader, range);

        for (var r = filaHeader + 1; r <= range.LastRow().RowNumber(); r++)
        {
            if (FilaVacia(hoja.Row(r))) continue;
            var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in definiciones)
            {
                if (!ResolverColumnaData(headerExacto, headerNormalizado, def.NombreVariable, out var col))
                {
                    valores[def.NombreVariable] = null;
                    continue;
                }
                var v = ObtenerTexto(hoja, r, col);
                valores[def.NombreVariable] = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }
            filas.Add(new DatosFilaDto(r, System.Text.Json.JsonSerializer.Serialize(valores)));
        }

        return filas;
    }

    private static OscValidacionResult Resultado(
        bool esValido,
        List<string> erroresDict,
        List<string> erroresData,
        List<string> observaciones,
        IReadOnlyList<CampoDiccionarioDto> campos,
        IReadOnlyList<DatosFilaDto> filas,
        GeografiaResumenDto? geografia) =>
        new(
            esValido,
            erroresDict,
            erroresData,
            observaciones,
            campos,
            filas,
            erroresDict.Count,
            erroresData.Count,
            geografia);

    private static IXLWorksheet? BuscarHojaDiccionario(IXLWorkbook wb) =>
        wb.Worksheets.FirstOrDefault(w =>
            w.Name.Contains("Diccionario", StringComparison.OrdinalIgnoreCase));

    private static IXLWorksheet? BuscarHojaData(IXLWorkbook wb)
    {
        var data = wb.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "DATA", StringComparison.OrdinalIgnoreCase));
        if (data is not null) return data;

        return wb.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "Datos", StringComparison.OrdinalIgnoreCase)
            || (w.Name.Contains("DATA", StringComparison.OrdinalIgnoreCase)
                && !w.Name.Contains("Diccionario", StringComparison.OrdinalIgnoreCase)));
    }

    private static string ObtenerTexto(IXLWorksheet hoja, int fila, int col)
    {
        var cell = hoja.Cell(fila, col);
        if (cell.IsEmpty()) return "";

        var texto = cell.GetString()?.Trim();
        if (!string.IsNullOrEmpty(texto)) return texto;

        if (cell.DataType == XLDataType.Number)
        {
            if (cell.TryGetValue(out long entero))
                return entero.ToString(CultureInfo.InvariantCulture);
            if (cell.TryGetValue(out double dbl))
                return ((long)dbl).ToString(CultureInfo.InvariantCulture);
        }

        var t = cell.GetFormattedString();
        if (string.IsNullOrWhiteSpace(t)) t = cell.Value.ToString();
        return (t ?? "").Trim();
    }

    private static bool FilaVacia(IXLRow row)
    {
        foreach (var cell in row.CellsUsed())
        {
            if (cell.IsEmpty()) continue;
            var t = cell.GetString()?.Trim();
            if (string.IsNullOrEmpty(t)) t = cell.GetFormattedString()?.Trim();
            if (!string.IsNullOrWhiteSpace(t)) return false;
        }
        return true;
    }

    private static bool EsSi(string raw)
    {
        var v = raw.Trim().ToUpperInvariant();
        return v is "S" or "SI" or "YES" or "Y" or "TRUE" or "1";
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static List<string> ParseDominiosTokens(string raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split([';', ',', '|', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    /// <summary>
    /// Solo valida dominio si el diccionario define una lista cerrada de valores (no texto de referencia a catálogos).
    /// </summary>
    private static bool EsTextoReglaDominio(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var lower = raw.Trim().ToLowerInvariant();

        if (EsReglaTextoAlfanumerico(lower))
            return true;

        string[] indicadoresNumericos =
        [
            "numero entero", "número entero", "entero mayor", "entero menor",
            "mayor o igual", "menor o igual", "mayor que", "menor que",
            "positivo", "negativo", "mayor a cero", "mayor a 0", "igual a cero",
            "solo numeros", "solo números", "sin decimales", "con decimales",
            "valor numerico", "valor numérico", "rango entre", "entre ", "hasta ", "desde "
        ];
        return indicadoresNumericos.Any(i => lower.Contains(i, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EsReglaTextoAlfanumerico(string lower) =>
        lower.Contains("alfanumer") || lower.Contains("no numerico") || lower.Contains("no numérico")
        || lower.Contains("texto") || lower.Contains("caracter") || lower.Contains("cadena")
        || lower.Contains("codigo no") || lower.Contains("código no");

    /// <summary>Letras (incl. tildes), números y signos habituales en observaciones/códigos de texto.</summary>
    private static bool EsTextoAlfanumerico(string valor) =>
        !string.IsNullOrWhiteSpace(valor)
        && Regex.IsMatch(valor, @"^[\p{L}\p{N}\s.,\-_/()#:;áéíóúÁÉÍÓÚñÑüÜ]+$");

    /// <summary>Evalúa reglas escritas en «Dominios» (ej. número entero ≥ 0 o código alfanumérico).</summary>
    private static bool CumpleReglaDominio(string valor, string regla, string tipoCanon)
    {
        var v = valor.Trim();
        var lower = regla.ToLowerInvariant();

        if (tipoCanon == "texto" || EsReglaTextoAlfanumerico(lower))
            return EsTextoAlfanumerico(v);

        var requiereEntero = tipoCanon == "entero"
            || ((lower.Contains("entero") || lower.Contains("sin decimal"))
                && !lower.Contains("no numerico") && !lower.Contains("no numérico"));
        var requiereDecimal = tipoCanon == "decimal" || lower.Contains("decimal");

        if (requiereEntero)
        {
            if (!long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var entero)
                && !long.TryParse(v, NumberStyles.Integer, CultureInfo.CurrentCulture, out entero))
                return false;

            if (lower.Contains("mayor o igual a cero") || lower.Contains("mayor o igual a 0")
                || lower.Contains(">= 0") || lower.Contains("≥ 0") || lower.Contains("no negativo"))
                return entero >= 0;

            if (lower.Contains("mayor que cero") || lower.Contains("mayor a cero")
                || lower.Contains("mayor que 0") || lower.Contains("> 0") || lower.Contains("positivo"))
                return entero > 0;

            if (lower.Contains("menor o igual a cero") || lower.Contains("<= 0"))
                return entero <= 0;

            return true;
        }

        if (requiereDecimal)
        {
            if (!decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec)
                && !decimal.TryParse(v, NumberStyles.Number, CultureInfo.CurrentCulture, out dec))
                return false;

            if (lower.Contains("mayor o igual a cero") || lower.Contains("mayor o igual a 0") || lower.Contains(">= 0"))
                return dec >= 0;

            if (lower.Contains("mayor que cero") || lower.Contains("> 0") || lower.Contains("positivo"))
                return dec > 0;

            return true;
        }

        if (lower.Contains("mayor o igual a cero") || lower.Contains("mayor o igual a 0"))
        {
            if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n >= 0;
            if (decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d >= 0;
            return false;
        }

        return true;
    }

    private static IReadOnlyList<string> InterpretarDominiosPermitidos(string raw, string tipoCanon, string nombreVariable)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        if (EsTextoReglaDominio(raw)) return [];

        var lower = raw.ToLowerInvariant();
        string[] palabrasReferencia =
        [
            "catalogo", "catálogo", "divipola", "dane", "tabla", "referencia", "según", "segun",
            "oficial", "consultar", "listado", "codigos", "códigos", "municipios de", "departamento",
            "ver ", "ejemplo", "rango", "entre", "hasta"
        ];
        if (palabrasReferencia.Any(p => lower.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return [];

        if (raw.Length > 250) return [];

        var tokens = ParseDominiosTokens(raw);
        if (tokens.Count == 0) return [];
        if (tokens.Count > 30) return [];
        if (tokens.Any(t => t.Length > 60)) return [];

        var norm = DiccionarioOscV2Reader.Normalizar(nombreVariable);

        if (norm.Contains("observacion") || norm.Contains("descripcion") || norm.Contains("comentario"))
            return [];

        if (tipoCanon == "texto")
            return [];

        if (norm.Contains("municipio") && tokens.Count > 12)
            return [];

        if (norm.Contains("poblacion") && (tokens.Count > 12 || tokens.Any(t => !Regex.IsMatch(t, @"^[\d.,]+$"))))
            return [];

        if (tipoCanon is "entero" or "decimal")
        {
            if (norm.Contains("ano") || norm.Contains("anio"))
            {
                if (tokens.All(t => Regex.IsMatch(t, @"^\d{4}$")))
                    return tokens;
                return [];
            }

            if (tokens.Any(t => !Regex.IsMatch(t, @"^[\d.,]+$")))
                return [];
        }

        if (norm.Contains("divipola") || norm.Contains("codigo"))
            return [];

        return tokens;
    }

    private static bool ValorEnDominio(string valor, DefinicionVariableOsc def)
    {
        var v = valor.Trim();
        if (def.Dominios.Contains(v, StringComparer.OrdinalIgnoreCase))
            return true;

        var norm = DiccionarioOscV2Reader.Normalizar(def.NombreVariable);
        if ((norm.Contains("ano") || norm.Contains("anio")) && int.TryParse(v, out var anio))
            return def.Dominios.Any(d => int.TryParse(d, out var y) && y == anio);

        return false;
    }

    private static string FormatearDominiosResumen(IReadOnlyList<string> dominios)
    {
        if (dominios.Count == 0) return "—";
        var muestra = dominios.Take(8).ToList();
        var texto = string.Join(", ", muestra);
        if (dominios.Count > 8) texto += $" … (+{dominios.Count - 8} más)";
        return texto;
    }

    private static bool EsLongitudFormatoNumericoSql(string raw) =>
        Regex.IsMatch(raw, @"p\s*\(\s*\d+\s*,\s*\d+\s*\)", RegexOptions.IgnoreCase);

    /// <summary>Longitud máxima en caracteres para validar DATA (no confundir p(10,2) SQL con tamaño de texto).</summary>
    private static int? InterpretarLongitudMaximaCaracteres(
        string? longStr, string tipoCanon, string nombreVariable, bool requiereNumerico)
    {
        var norm = DiccionarioOscV2Reader.Normalizar(nombreVariable);

        if (requiereNumerico && int.TryParse(longStr?.Trim(), out var nDic) && nDic >= 0)
            return nDic;

        if (string.IsNullOrWhiteSpace(longStr))
            return requiereNumerico ? InferirLongitudPorNombre(nombreVariable) : null;

        if (norm.Contains("divipola") || (norm.Contains("codigo") && norm.Contains("divipola")))
            return requiereNumerico ? (int.TryParse(longStr.Trim(), out var nd) ? nd : 8) : null;

        if (norm.Contains("ano") || norm.Contains("anio"))
            return 4;

        if (EsLongitudFormatoNumericoSql(longStr))
        {
            if (requiereNumerico)
                return InferirLongitudPorNombre(nombreVariable);
            return null;
        }

        if (int.TryParse(longStr.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0)
        {
            if (requiereNumerico)
            {
                if (norm.Contains("ano") || norm.Contains("anio")) return 4;
                if (norm.Contains("divipola")) return n;
                return n;
            }
            return n;
        }

        return requiereNumerico ? InferirLongitudPorNombre(nombreVariable) : null;
    }

    private static int? InferirLongitudPorNombre(string nombreVariable)
    {
        var norm = DiccionarioOscV2Reader.Normalizar(nombreVariable);
        if (norm.Contains("divipola")) return 8;
        if (norm.Contains("ano") || norm.Contains("anio")) return 4;
        return null;
    }

    private static (Dictionary<string, int> Exacto, Dictionary<string, int> Normalizado) ConstruirMapasEncabezadosData(
        IXLWorksheet hoja,
        int filaHeader,
        IXLRange range)
    {
        var exacto = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizado = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var c = range.FirstColumn().ColumnNumber(); c <= range.LastColumn().ColumnNumber(); c++)
        {
            var titulo = ObtenerTexto(hoja, filaHeader, c);
            if (string.IsNullOrWhiteSpace(titulo)) continue;
            var t = titulo.Trim();
            exacto[t] = c;
            var norm = DiccionarioOscV2Reader.Normalizar(t);
            if (!string.IsNullOrEmpty(norm))
                normalizado[norm] = c;
        }
        return (exacto, normalizado);
    }

    private static bool ResolverColumnaData(
        IReadOnlyDictionary<string, int> exacto,
        IReadOnlyDictionary<string, int> normalizado,
        string nombreVariable,
        out int columna)
    {
        if (exacto.TryGetValue(nombreVariable.Trim(), out columna))
            return true;

        var normVar = DiccionarioOscV2Reader.Normalizar(nombreVariable);
        if (normVar.Length > 0 && normalizado.TryGetValue(normVar, out columna))
            return true;

        foreach (var kv in normalizado)
        {
            if (SonNombresColumnaEquivalentes(normVar, kv.Key))
            {
                columna = kv.Value;
                return true;
            }
        }

        columna = 0;
        return false;
    }

    /// <summary>Coincidencia estricta (sin confundir MUNICIPIO con CODIGO DIVIPOLA).</summary>
    private static bool SonNombresColumnaEquivalentes(string normVar, string normHeader)
    {
        if (normVar == normHeader) return true;
        if (normVar.Length < 4 || normHeader.Length < 4) return false;
        return normHeader.StartsWith(normVar + "_", StringComparison.Ordinal)
            || normVar.StartsWith(normHeader + "_", StringComparison.Ordinal);
    }

    private static string MensajeTipoInvalido(int fila, DefinicionVariableOsc def, string valor)
    {
        var norm = DiccionarioOscV2Reader.Normalizar(def.NombreVariable);
        if (def.RequiereNumerico || def.TipoCanon is "entero" or "decimal")
        {
            if (norm.Contains("ano") || norm.Contains("anio"))
                return $"Fila {fila}, columna {def.NombreVariable}: el año debe ser numérico (solo dígitos, ej. 2024). No use texto como «{valor}».";
            if (norm.Contains("divipola"))
                return $"Fila {fila}, columna {def.NombreVariable}: el código DIVIPOLA debe ser numérico (solo dígitos). Valor rechazado: «{valor}».";
            if (norm.Contains("poblacion"))
                return $"Fila {fila}, columna {def.NombreVariable}: la población debe ser numérica (solo dígitos). Valor rechazado: «{valor}».";
            var tipoDic = string.IsNullOrWhiteSpace(def.TipoDatoDiccionario) ? "Numérico" : def.TipoDatoDiccionario;
            return $"Fila {fila}, columna {def.NombreVariable}: según el diccionario el tipo es «{tipoDic}»; el valor «{valor}» debe ser numérico (solo dígitos).";
        }
        if (def.TipoCanon == "fecha" && norm.Contains("fecha"))
            return $"Fila {fila}, columna {def.NombreVariable}: debe ser una fecha válida.";
        if (def.TipoCanon == "texto" || norm.Contains("observ") || norm.Contains("coment"))
            return $"Fila {fila}, columna {def.NombreVariable}: debe ser texto alfanumérico (letras y números permitidos).";
        return $"Fila {fila}, columna {def.NombreVariable}: tipo de dato inválido. Se esperaba {def.TipoEtiqueta}.";
    }

    private static bool EsCampoAnio(string nombreVariable)
    {
        var n = DiccionarioOscV2Reader.Normalizar(nombreVariable);
        return n.Contains("ano") || n.Contains("anio") || n == "year" || n.StartsWith("ano_");
    }

    private static bool EsCampoNumericoPorNombre(string nombreVariable)
    {
        var n = DiccionarioOscV2Reader.Normalizar(nombreVariable);
        if (n.Contains("observ") || n.Contains("coment") || n.Contains("descrip")
            || n.Contains("sexo") || n.Contains("municipio")) return false;
        return n.Contains("divipola") || n.Contains("poblacion")
            || n.Contains("ano") || n.Contains("anio");
    }

    private static int BuscarFilaEncabezadosData(
        IXLWorksheet hoja,
        IReadOnlyList<DefinicionVariableOsc> definiciones,
        IXLRange range)
    {
        var varsNorm = definiciones
            .Select(d => DiccionarioOscV2Reader.Normalizar(d.NombreVariable))
            .Where(s => s.Length > 1)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var bestRow = range.FirstRow().RowNumber();
        var bestScore = 0;
        var maxRow = Math.Min(hoja.LastRowUsed()?.RowNumber() ?? bestRow, bestRow + 25);

        for (var r = range.FirstRow().RowNumber(); r <= maxRow; r++)
        {
            var score = 0;
            var maxCol = hoja.LastColumnUsed()?.ColumnNumber() ?? range.LastColumn().ColumnNumber();
            for (var c = range.FirstColumn().ColumnNumber(); c <= maxCol; c++)
            {
                var norm = DiccionarioOscV2Reader.Normalizar(ObtenerTexto(hoja, r, c));
                if (varsNorm.Contains(norm)) score++;
            }
            if (score > bestScore)
            {
                bestScore = score;
                bestRow = r;
            }
        }

        return bestScore >= 2 ? bestRow : range.FirstRow().RowNumber();
    }

    /// <summary>Relaciona cada columna DATA con su definición (solo coincidencia exacta de nombre).</summary>
    private static Dictionary<int, DefinicionVariableOsc> ConstruirMapaValidacionPorColumna(
        IReadOnlyDictionary<string, int> headerExacto,
        IReadOnlyDictionary<string, int> headerNormalizado,
        IReadOnlyList<DefinicionVariableOsc> definiciones,
        List<string> errores)
    {
        var mapa = new Dictionary<int, DefinicionVariableOsc>();
        var columnasUsadas = new HashSet<int>();

        foreach (var def in definiciones)
        {
            if (!ResolverColumnaData(headerExacto, headerNormalizado, def.NombreVariable, out var col)
                || !columnasUsadas.Add(col))
            {
                errores.Add($"Falta la columna «{def.NombreVariable}» en la hoja DATA (según Diccionario_datos).");
                continue;
            }
            mapa[col] = def;
        }

        return mapa;
    }

    private static bool DebeValidarComoNumerico(string? tipoRaw, string tipoCanon, string nombreVariable)
    {
        if (EsTipoNumericoDiccionario(tipoRaw)) return true;
        if (tipoCanon is "entero" or "decimal") return true;
        if (EsTipoTextoDiccionario(tipoRaw)) return false;
        return EsCampoNumericoPorNombre(nombreVariable);
    }

    private static bool EsTipoNumericoDiccionario(string? tipoRaw)
    {
        if (string.IsNullOrWhiteSpace(tipoRaw)) return false;
        var t = DiccionarioOscV2Reader.Normalizar(tipoRaw);
        return t is "numerico" or "numero" or "entero" or "int" or "integer"
            || t.Contains("numer") || t.Contains("entero") || t.Contains("numero")
            || t.Contains("int") || t.Contains("decimal");
    }

    private static bool EsTipoTextoDiccionario(string? tipoRaw)
    {
        if (string.IsNullOrWhiteSpace(tipoRaw)) return false;
        var t = DiccionarioOscV2Reader.Normalizar(tipoRaw);
        return (t.Contains("texto") || t.Contains("alfan") || t.Contains("cadena") || t.Contains("caracter"))
            && !t.Contains("numer") && !t.Contains("numero");
    }

    private static string MapearTipoDatoCanon(string raw, string nombreVariable)
    {
        var normNombre = DiccionarioOscV2Reader.Normalizar(nombreVariable);

        if (normNombre.Contains("observ") || normNombre.Contains("coment") || normNombre.Contains("descrip"))
            return "texto";

        if (EsTipoNumericoDiccionario(raw) || EsCampoNumericoPorNombre(nombreVariable))
            return "entero";

        var t = DiccionarioOscV2Reader.Normalizar(raw);

        if (string.IsNullOrWhiteSpace(t))
        {
            if (normNombre.Contains("fecha")) return "fecha";
            if (EsCampoNumericoPorNombre(nombreVariable)) return "entero";
            return "texto";
        }

        if (t.Contains("fecha")) return "fecha";
        if (t.Contains("bool") || t.Contains("logico")) return "booleano";
        if (t.Contains("decimal") || t.Contains("double") || t.Contains("float")) return "decimal";
        if (EsTipoNumericoDiccionario(raw)) return "entero";
        if (EsTipoTextoDiccionario(raw)) return "texto";

        return "texto";
    }

    private static string EtiquetaTipo(string raw, string canon) =>
        string.IsNullOrWhiteSpace(raw) ? canon switch
        {
            "entero" => "Numérico",
            "decimal" => "Decimal",
            "fecha" => "Fecha",
            "booleano" => "Booleano",
            _ => "Texto"
        } : raw.Trim();

    private static bool ValidarTipo(string valor, DefinicionVariableOsc def, out string tipoEsperado)
    {
        var exigeNumerico = def.RequiereNumerico || EsCampoNumericoPorNombre(def.NombreVariable);
        if (exigeNumerico)
        {
            tipoEsperado = "Numérico";
            return EsValorNumericoEntero(valor);
        }

        var tipoCanon = def.TipoCanon;

        tipoEsperado = tipoCanon switch
        {
            "entero" => "Numérico",
            "decimal" => "Decimal",
            "fecha" => "Fecha",
            "booleano" => "Booleano",
            _ => "Texto"
        };

        return tipoCanon switch
        {
            "entero" => EsValorNumericoEntero(valor),
            "decimal" => EsValorNumericoDecimal(valor),
            "fecha" => DateTime.TryParse(valor, CultureInfo.CurrentCulture, DateTimeStyles.None, out _)
                || DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "booleano" => EsBooleano(valor),
            "texto" => EsTextoAlfanumerico(valor),
            _ => EsTextoAlfanumerico(valor)
        };
    }

    /// <summary>Solo dígitos (rechaza letras, espacios y texto como «dos mil veinticuatro»).</summary>
    private static bool EsValorNumericoEntero(string valor)
    {
        var v = valor.Trim();
        if (string.IsNullOrEmpty(v)) return false;
        if (Regex.IsMatch(v, @"\p{L}")) return false;
        if (!Regex.IsMatch(v, @"^\d+$")) return false;
        return long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool EsValorNumericoDecimal(string valor)
    {
        var v = valor.Trim();
        if (string.IsNullOrEmpty(v)) return false;
        if (!Regex.IsMatch(v, @"^-?\d+([.,]\d+)?$")) return false;
        return decimal.TryParse(v.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out _);
    }

    private static bool EsBooleano(string valor)
    {
        var v = valor.Trim().ToUpperInvariant();
        return v is "S" or "N" or "SI" or "NO" or "TRUE" or "FALSE" or "1" or "0"
            or "Y" or "YES";
    }

    private sealed record DefinicionVariableOsc(
        int FilaDiccionario,
        string NombreVariable,
        string TipoDatoDiccionario,
        string TipoCanon,
        string TipoEtiqueta,
        bool RequiereNumerico,
        bool ObligatorioEnData,
        bool EsLlavePrimaria,
        bool EsCalculado,
        int? LongitudMaxima,
        IReadOnlyList<string> Dominios,
        string? DominioRegla,
        CampoDiccionarioDto Campo);
}

public sealed record OscValidacionResult(
    bool EsValido,
    IReadOnlyList<string> ErroresDiccionario,
    IReadOnlyList<string> ErroresData,
    IReadOnlyList<string> Observaciones,
    IReadOnlyList<CampoDiccionarioDto> Campos,
    IReadOnlyList<DatosFilaDto> Filas,
    int TotalErroresDiccionario,
    int TotalErroresData,
    GeografiaResumenDto? Geografia)
{
    public IReadOnlyList<string> TodosLosErrores =>
        ErroresDiccionario.Concat(ErroresData).ToList();
}
