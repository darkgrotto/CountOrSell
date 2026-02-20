namespace MtgHelper.Core.Services;

public static class KeyruneMapper
{
    private static readonly Dictionary<string, char> SetSymbols = new()
    {
        { "lea", '\ue600' }, { "leb", '\ue601' }, { "2ed", '\ue602' }, { "3ed", '\ue603' },
        { "4ed", '\ue604' }, { "psum", '\ue605' }, { "5ed", '\ue606' }, { "6ed", '\ue607' },
        { "7ed", '\ue608' }, { "8ed", '\ue609' }, { "9ed", '\ue60a' }, { "10e", '\ue60b' },
        { "m10", '\ue60c' }, { "m11", '\ue60d' }, { "m12", '\ue60e' }, { "m13", '\ue60f' },
        { "m14", '\ue610' }, { "m15", '\ue611' }, { "bcore", '\ue612' }, { "ori", '\ue697' },
        { "m19", '\ue941' }, { "m20", '\ue95d' }, { "1e", '\ue947' }, { "2e", '\ue948' },
        { "2u", '\ue949' }, { "3e", '\ue94a' }, { "m21", '\ue960' }, { "xdnd", '\ue972' },
        { "afr", '\ue972' }, { "fdn", '\ue9d8' },
        { "arn", '\ue613' }, { "atq", '\ue614' }, { "leg", '\ue615' }, { "drk", '\ue616' },
        { "fem", '\ue617' }, { "hml", '\ue618' }, { "ice", '\ue619' }, { "ice2", '\ue925' },
        { "all", '\ue61a' }, { "csp", '\ue61b' }, { "mir", '\ue61c' }, { "vis", '\ue61d' },
        { "wth", '\ue61e' }, { "tmp", '\ue61f' }, { "sth", '\ue620' }, { "exo", '\ue621' },
        { "usg", '\ue622' }, { "ulg", '\ue623' }, { "uds", '\ue624' }, { "mmq", '\ue625' },
        { "nem", '\ue626' }, { "nms", '\ue626' }, { "pcy", '\ue627' }, { "inv", '\ue628' },
        { "pls", '\ue629' }, { "apc", '\ue62a' }, { "ody", '\ue62b' }, { "tor", '\ue62c' },
        { "jud", '\ue62d' }, { "ons", '\ue62e' }, { "lgn", '\ue62f' }, { "scg", '\ue630' },
        { "mrd", '\ue631' }, { "dst", '\ue632' }, { "5dn", '\ue633' }, { "chk", '\ue634' },
        { "bok", '\ue635' }, { "sok", '\ue636' }, { "rav", '\ue637' }, { "gpt", '\ue638' },
        { "dis", '\ue639' }, { "tsp", '\ue63a' }, { "plc", '\ue63b' }, { "fut", '\ue63c' },
        { "lrw", '\ue63d' }, { "mor", '\ue63e' }, { "shm", '\ue63f' }, { "eve", '\ue640' },
        { "ala", '\ue641' }, { "con", '\ue642' }, { "arb", '\ue643' }, { "zen", '\ue644' },
        { "wwk", '\ue645' }, { "roe", '\ue646' }, { "som", '\ue647' }, { "mbs", '\ue648' },
        { "nph", '\ue649' }, { "isd", '\ue64a' }, { "dka", '\ue64b' }, { "avr", '\ue64c' },
        { "rtr", '\ue64d' }, { "gtc", '\ue64e' }, { "dgm", '\ue64f' }, { "ths", '\ue650' },
        { "bng", '\ue651' }, { "jou", '\ue652' }, { "ktk", '\ue653' }, { "frf", '\ue654' },
        { "dtk", '\ue693' }, { "bfz", '\ue699' }, { "ogw", '\ue901' }, { "soi", '\ue902' },
        { "emn", '\ue90b' }, { "kld", '\ue90e' }, { "aer", '\ue90f' }, { "akh", '\ue914' },
        { "hou", '\ue924' }, { "xln", '\ue92e' }, { "rix", '\ue92f' }, { "dom", '\ue93f' },
        { "grn", '\ue94b' }, { "gk1", '\ue94b' }, { "rna", '\ue959' }, { "gk2", '\ue959' },
        { "war", '\ue95a' }, { "eld", '\ue95e' }, { "thb", '\ue961' }, { "iko", '\ue962' },
        { "znr", '\ue963' }, { "khm", '\ue974' }, { "stx", '\ue975' }, { "mid", '\ue978' },
        { "vow", '\ue977' }, { "neo", '\ue98c' }, { "snc", '\ue98b' }, { "dmu", '\ue993' },
        { "bro", '\ue99d' }, { "one", '\ue9a1' }, { "mom", '\ue9a2' }, { "mat", '\ue9a3' },
        { "woe", '\ue9ae' }, { "lci", '\ue9c2' }, { "mkm", '\ue9c9' }, { "otj", '\ue9cc' },
        { "blb", '\ue9cd' }, { "dsk", '\ue9d7' }, { "dft", '\ue9e0' }, { "tdm", '\ue9e6' },
        { "fin", '\ue9ed' }, { "eoe", '\ue9f0' }, { "spm", '\ue9f1' }, { "tla", '\ue9fb' },
        { "van", '\ue655' }, { "hop", '\ue656' }, { "arc", '\ue657' }, { "cmd", '\ue658' },
        { "pc2", '\ue659' }, { "cm1", '\ue65a' }, { "c13", '\ue65b' }, { "cns", '\ue65c' },
        { "c14", '\ue65d' }, { "c15", '\ue900' }, { "cn2", '\ue904' }, { "c16", '\ue9e5' },
        { "pca", '\ue911' }, { "cma", '\ue916' }, { "e01", '\ue92d' }, { "ann", '\ue92d' },
        { "e02", '\ue931' }, { "c17", '\ue934' }, { "cm2", '\ue940' }, { "bbd", '\ue942' },
        { "c18", '\ue946' }, { "c19", '\ue95f' }, { "c20", '\ue966' }, { "znc", '\ue967' },
        { "cc1", '\ue968' }, { "cmr", '\ue969' }, { "cmc", '\ue969' }, { "khc", '\ue97d' },
        { "c21", '\ue97e' }, { "afc", '\ue981' }, { "mic", '\ue985' }, { "voc", '\ue986' },
        { "cc2", '\ue987' }, { "nec", '\ue98d' }, { "ncc", '\ue98e' }, { "clb", '\ue991' },
        { "dmc", '\ue994' }, { "40k", '\ue998' }, { "brc", '\ue99f' }, { "onc", '\ue9a8' },
        { "moc", '\ue9a9' }, { "scd", '\ue9ab' }, { "cmm", '\ue9b5' }, { "ltc", '\ue9b6' },
        { "woc", '\ue9b9' }, { "lcc", '\ue9c7' }, { "mkc", '\ue9ca' }, { "otc", '\ue9d2' },
        { "blc", '\ue9d4' }, { "m3c", '\ue9d0' }, { "dsc", '\ue9dc' }, { "fdc", '\ue9e4' },
        { "drc", '\ue9e8' }, { "tdc", '\ue9f4' }, { "fic", '\ue9f5' }, { "eoc", '\ue9f6' },
        { "chr", '\ue65e' }, { "ath", '\ue65f' }, { "brb", '\ue660' }, { "btd", '\ue661' },
        { "dkm", '\ue662' }, { "mma", '\ue663' }, { "mm2", '\ue695' }, { "ema", '\ue903' },
        { "mm3", '\ue912' }, { "xren", '\ue917' }, { "xrin", '\ue918' }, { "ima", '\ue935' },
        { "a25", '\ue93d' }, { "uma", '\ue958' }, { "mh1", '\ue95b' }, { "2xm", '\ue96e' },
        { "jmp", '\ue96f' }, { "mb1", '\ue971' }, { "mh2", '\ue97b' }, { "sta", '\ue980' },
        { "j21", '\ue983' }, { "2x2", '\ue99c' }, { "brr", '\ue9a0' }, { "j22", '\ue9ad' },
        { "mul", '\ue9ba' }, { "wot", '\ue9c0' }, { "br", '\ue9c1' }, { "spg", '\ue9c8' },
        { "otp", '\ue9d5' }, { "big", '\ue9d6' }, { "h2r", '\ue97b' }, { "mb2", '\ue9d9' },
        { "j25a", '\ue9db' }, { "j25", '\ue9df' }, { "pio", '\ue9e7' }, { "fca", '\ue9f8' },
        { "mar", '\ue9f6' },
        { "por", '\ue664' }, { "p02", '\ue665' }, { "po2", '\ue665' }, { "ptk", '\ue666' },
        { "s99", '\ue667' }, { "s00", '\ue668' }, { "w16", '\ue907' }, { "w17", '\ue923' },
        { "evg", '\ue669' }, { "dd2", '\ue66a' }, { "ddc", '\ue66b' }, { "ddd", '\ue66c' },
        { "dde", '\ue66d' }, { "ddf", '\ue66e' }, { "ddg", '\ue66f' }, { "ddh", '\ue670' },
        { "ddi", '\ue671' }, { "ddj", '\ue672' }, { "ddk", '\ue673' }, { "ddl", '\ue674' },
        { "ddm", '\ue675' }, { "ddn", '\ue676' }, { "ddo", '\ue677' }, { "ddp", '\ue698' },
        { "ddq", '\ue908' }, { "ddr", '\ue90d' }, { "td2", '\ue91c' }, { "dds", '\ue921' },
        { "ddt", '\ue933' }, { "ddu", '\ue93e' },
        { "drb", '\ue678' }, { "v09", '\ue679' }, { "v10", '\ue67a' }, { "v11", '\ue67b' },
        { "v12", '\ue67c' }, { "v13", '\ue67d' }, { "v14", '\ue67e' }, { "v15", '\ue905' },
        { "v16", '\ue906' }, { "v0x", '\ue920' }, { "v17", '\ue939' },
        { "h09", '\ue67f' }, { "pd2", '\ue680' }, { "pd3", '\ue681' }, { "md1", '\ue682' },
        { "ss1", '\ue944' }, { "ss2", '\ue95c' }, { "ss3", '\ue96d' }, { "gs1", '\ue945' },
        { "azorius", '\ue94e' }, { "boros", '\ue94f' }, { "dimir", '\ue950' },
        { "golgari", '\ue951' }, { "gruul", '\ue952' }, { "izzet", '\ue953' },
        { "orzhov", '\ue954' }, { "rakdos", '\ue955' }, { "selesnya", '\ue956' },
        { "simic", '\ue957' },
        { "gnt", '\ue94d' }, { "gn2", '\ue964' }, { "tsr", '\ue976' }, { "dmr", '\ue9a4' },
        { "gn3", '\ue9a5' }, { "ltr", '\ue9af' }, { "who", '\ue9b0' }, { "rvr", '\ue9bb' },
        { "pip", '\ue9c3' }, { "clu", '\ue9cb' }, { "acr", '\ue9ce' }, { "mh3", '\ue9cf' },
        { "inr", '\ue9e2' }, { "spe", '\ue9f3' },
        { "pgru", '\ue683' }, { "pmtg1", '\ue684' }, { "pmtg2", '\ue685' },
        { "pleaf", '\ue686' }, { "pmei", '\ue687' }, { "parl", '\ue688' },
        { "dpa", '\ue689' }, { "pbook", '\ue68a' }, { "past", '\ue68b' },
        { "parl2", '\ue68c' }, { "exp", '\ue69a' }, { "psalvat05", '\ue909' },
        { "psalvat11", '\ue90a' }, { "mp1", '\ue913' }, { "mps", '\ue913' },
        { "pxbox", '\ue915' }, { "pmps", '\ue919' }, { "pmpu", '\ue91a' },
        { "mp2", '\ue922' }, { "pidw", '\ue92c' }, { "pdrc", '\ue932' },
        { "pheart", '\ue936' }, { "h17", '\ue938' }, { "pdep", '\ue93a' },
        { "psega", '\ue93b' }, { "ptsa", '\ue93c' }, { "parl3", '\ue943' },
        { "htr", '\ue687' }, { "med", '\ue94c' }, { "ptg", '\ue965' },
        { "htr17", '\ue687' }, { "j20", '\ue96a' }, { "zne", '\ue97a' },
        { "bot", '\ue99e' }, { "rex", '\ue9c4' }, { "eos", '\ue9f0' },
        { "slu", '\ue687' }, { "sld", '\ue687' }, { "psld", '\ue687' }, { "sld2", '\ue9bc' },
        { "me1", '\ue68d' }, { "me2", '\ue68e' }, { "me3", '\ue68f' }, { "me4", '\ue690' },
        { "tpr", '\ue694' }, { "vma", '\ue696' }, { "xlcu", '\ue90c' }, { "pz1", '\ue90c' },
        { "modo", '\ue91b' }, { "pmodo", '\ue91b' }, { "duels", '\ue91d' },
        { "xduels", '\ue91d' }, { "xmods", '\ue91e' }, { "pz2", '\ue91f' },
        { "ha1", '\ue96b' }, { "akr", '\ue970' }, { "klr", '\ue97c' },
        { "y22", '\ue989' }, { "hbg", '\ue9a6' }, { "y23", '\ue9a7' },
        { "ydmu", '\ue9a7' }, { "sir", '\ue9b1' }, { "sis", '\ue9b2' },
        { "ea1", '\ue9b4' }, { "y24", '\ue9bd' }, { "y25", '\ue9da' },
        { "yblb", '\ue9da' }, { "pma", '\ue9f0' }, { "pm2", '\ue9f0' }, { "dvk", '\ue9f0' },
        { "ugl", '\ue691' }, { "unh", '\ue692' }, { "ust", '\ue930' },
        { "und", '\ue96c' }, { "unf", '\ue98a' }, { "una", '\ue9be' },
        { "xcle", '\ue926' }, { "xice", '\ue927' }, { "x2ps", '\ue928' },
        { "x4ea", '\ue929' }, { "papac", '\ue92a' }, { "peuro", '\ue92b' },
        { "pfnm", '\ue937' }, { "30a", '\ue9aa' },
    };

    public static char? GetSymbol(string setCode)
    {
        if (string.IsNullOrEmpty(setCode))
            return null;

        return SetSymbols.TryGetValue(setCode.ToLowerInvariant(), out var symbol)
            ? symbol
            : null;
    }

    public static bool HasSymbol(string setCode)
    {
        if (string.IsNullOrEmpty(setCode))
            return false;

        return SetSymbols.ContainsKey(setCode.ToLowerInvariant());
    }
}
