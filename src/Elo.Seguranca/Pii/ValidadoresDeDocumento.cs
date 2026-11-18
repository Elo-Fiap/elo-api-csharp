namespace Elo.Seguranca.Pii;

/// <summary>Dígitos verificadores de CPF e do Cartão Nacional de Saúde (CNS): evitam confundir qualquer número longo com um documento.</summary>
internal static class ValidadoresDeDocumento
{
    public static string SoDigitos(string texto)
    {
        Span<char> destino = stackalloc char[Math.Min(texto.Length, 64)];
        var n = 0;
        foreach (var c in texto)
        {
            if (c is >= '0' and <= '9' && n < destino.Length)
            {
                destino[n++] = c;
            }
        }

        return new string(destino[..n]);
    }

    /// <summary>11 dígitos, não todos iguais, com os dois dígitos verificadores corretos.</summary>
    public static bool CpfValido(string digitos)
    {
        if (digitos.Length != 11 || digitos.All(c => c == digitos[0]))
        {
            return false;
        }

        return DigitoDoCpf(digitos, 9) == digitos[9] - '0' && DigitoDoCpf(digitos, 10) == digitos[10] - '0';
    }

    private static int DigitoDoCpf(string digitos, int quantos)
    {
        var soma = 0;
        for (var i = 0; i < quantos; i++)
        {
            soma += (digitos[i] - '0') * (quantos + 1 - i);
        }

        var resto = soma * 10 % 11;
        return resto == 10 ? 0 : resto;
    }

    /// <summary>
    /// CNS com 15 dígitos: começa com 1 ou 2 (definitivo, derivado do PIS) ou 7, 8 ou 9 (provisório, soma ponderada múltipla de 11).
    /// </summary>
    public static bool CnsValido(string digitos)
    {
        if (digitos.Length != 15)
        {
            return false;
        }

        if (digitos[0] is '1' or '2')
        {
            var soma = 0;
            for (var i = 0; i < 11; i++)
            {
                soma += (digitos[i] - '0') * (15 - i);
            }

            var dv = 11 - soma % 11;
            if (dv == 11)
            {
                dv = 0;
            }

            string esperado;
            if (dv == 10)
            {
                soma += 2;
                dv = 11 - soma % 11;
                esperado = digitos[..11] + "001" + dv;
            }
            else
            {
                esperado = digitos[..11] + "000" + dv;
            }

            return esperado == digitos;
        }

        if (digitos[0] is '7' or '8' or '9')
        {
            var soma = 0;
            for (var i = 0; i < 15; i++)
            {
                soma += (digitos[i] - '0') * (15 - i);
            }

            return soma % 11 == 0;
        }

        return false;
    }
}
