using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Bankszamlak;

public enum PenzfelvetelEredmeny
{
    Sikeres,
    ErvenytelenOsszeg,
    LimitTullepve,
    NincsElegFedezet
}

public class Bankszamla
{
    // A leszármazottak sem írhatják át közvetlenül az egyenleget.
    private decimal egyenleg; //backing field, háttérmező, a tulajdonság értékét tárolja
    private decimal felveteliLimit;

    public string Tulajdonos { get; } //csak a konstruktor adhat neki értéket
    //public string Tulajdonos { get; private set; } //Ha így lenne, az osztály saját metódusai később is módosíthatnák az értéket.
    public decimal Egyenleg => egyenleg;

    public decimal FelveteliLimit
    {
        get => felveteliLimit;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(FelveteliLimit),
                    "A felvételi limitnek pozitívnak kell lennie.");
            //nameof(FelveteliLimit) eredménye: FelveteliLimit - szövegesen
            felveteliLimit = value;
        }
    }

    public Bankszamla(string tulajdonos, decimal kezdoegyenleg, decimal limit)
    {
        if (string.IsNullOrWhiteSpace(tulajdonos))
            throw new ArgumentException("A tulajdonos neve nem lehet üres.", nameof(tulajdonos));

        if (kezdoegyenleg < 0)
            throw new ArgumentOutOfRangeException(nameof(kezdoegyenleg),
                "A kezdőegyenleg nem lehet negatív.");

        Tulajdonos = tulajdonos.Trim();
        egyenleg = kezdoegyenleg;
        FelveteliLimit = limit; // A setter ellenőrzését használjuk.
    }

    // protected: az osztály és a leszármazottai használhatják.
    protected void Jovair(decimal osszeg)
    {
        if (osszeg <= 0)
            throw new ArgumentOutOfRangeException(nameof(osszeg),
                "A jóváírás összegének pozitívnak kell lennie.");

        egyenleg += osszeg;
    }

    public virtual void Befizet(decimal osszeg)
    {
        Jovair(osszeg);
    }

    // Egyetlen műveletben ellenőrizzük és vonjuk le a teljes terhelést.
    // Ez a segédmetódus nem vizsgál pénzfelvételi limitet.
    protected PenzfelvetelEredmeny MegprobalTerhelni(decimal osszeg)
    {
        if (osszeg <= 0)
            return PenzfelvetelEredmeny.ErvenytelenOsszeg;

        if (osszeg > egyenleg)
            return PenzfelvetelEredmeny.NincsElegFedezet;

        egyenleg -= osszeg;
        return PenzfelvetelEredmeny.Sikeres;
    }

    public virtual PenzfelvetelEredmeny Felvesz(decimal osszeg)
    {
        if (osszeg <= 0)
            return PenzfelvetelEredmeny.ErvenytelenOsszeg;

        if (osszeg > FelveteliLimit)
            return PenzfelvetelEredmeny.LimitTullepve;

        return MegprobalTerhelni(osszeg);
    }

    public override string ToString()
    {
        return $"{GetType().Name} | {Tulajdonos} | " +
               $"Egyenleg: {Egyenleg:N2} Ft | Limit: {FelveteliLimit:N2} Ft";
        //A GetType().Name az objektum tényleges osztályának nevét adja vissza szövegként. (pl. Bankszamla, Diakszamla,stb.)
    }
}

public class Diakszamla : Bankszamla
{
    public string Iskola { get; }

    // A base hívás hozza létre és ellenőrzi az ősosztály adatait.
    public Diakszamla(string tulajdonos, decimal kezdoegyenleg, string iskola)
        : base(tulajdonos, kezdoegyenleg, 20000) //implicit típuskonverzió decimalra
    {
        if (string.IsNullOrWhiteSpace(iskola))
            throw new ArgumentException("Az iskola neve nem lehet üres.", nameof(iskola));

        Iskola = iskola.Trim();
    }

    public override string ToString()
    {
        return $"{base.ToString()} | Iskola: {Iskola}";
    }
}

public class PremiumSzamla : Bankszamla
{
    public decimal BonuszSzazalek { get; }

    public PremiumSzamla(string tulajdonos, decimal kezdoegyenleg,
        decimal limit, decimal bonuszSzazalek)
        : base(tulajdonos, kezdoegyenleg, limit)
    {
        if (bonuszSzazalek < 0 || bonuszSzazalek > 10)
            throw new ArgumentOutOfRangeException(nameof(bonuszSzazalek),
                "A bónuszszázalék 0 és 10 közötti lehet.");

        BonuszSzazalek = bonuszSzazalek;
    }

    public override void Befizet(decimal osszeg)
    {
        // A konstruktor nem ezt hívja: a kezdőegyenlegre nem jár bónusz.
        // Előre kiszámítjuk a bónuszt, majd az ős ellenőrzi a befizetést.
        decimal bonusz = Math.Round(osszeg * BonuszSzazalek / 100m,
            2, MidpointRounding.AwayFromZero);

        base.Befizet(osszeg);// Az alapösszeg már növeli az egyenleget.

        // Nulla bónusznál nincs külön jóváírás.
        if (bonusz > 0)// Ha itt túlcsordulás történik,
                       // az alapösszeg jóváírása már megtörtént.
            Jovair(bonusz);
    }

    public override string ToString()
    {
        return $"{base.ToString()} | Bónusz: {BonuszSzazalek:0.##}%";
    }
}

public class UzletiSzamla : Bankszamla
{
    public string Cegnev { get; }
    public decimal PenzfelveteliDij { get; }

    public UzletiSzamla(string tulajdonos, decimal kezdoegyenleg,
        decimal limit, string cegnev, decimal penzfelveteliDij)
        : base(tulajdonos, kezdoegyenleg, limit)
    {
        if (string.IsNullOrWhiteSpace(cegnev))
            throw new ArgumentException("A cégnév nem lehet üres.", nameof(cegnev));

        if (penzfelveteliDij < 0)
            throw new ArgumentOutOfRangeException(nameof(penzfelveteliDij),
                "A pénzfelvételi díj nem lehet negatív.");

        Cegnev = cegnev.Trim();
        PenzfelveteliDij = penzfelveteliDij;
    }

    public override PenzfelvetelEredmeny Felvesz(decimal osszeg)
    {
        if (osszeg <= 0)
            return PenzfelvetelEredmeny.ErvenytelenOsszeg;

        // A díj nem számít bele a felvételi limitbe.
        if (osszeg > FelveteliLimit)
            return PenzfelvetelEredmeny.LimitTullepve;

        // Nem hívjuk a base.Felvesz metódust, mert az már levonná
        // a pénzt a díj fedezetének ellenőrzése előtt.
        return MegprobalTerhelni(osszeg + PenzfelveteliDij);
    }

    public override string ToString()
    {
        return $"{base.ToString()} | Cég: {Cegnev} | Díj: {PenzfelveteliDij:N2} Ft";
    }
}

public static class Program
{
    public static void Main()
    {
        //Console.OutputEncoding = Encoding.UTF8; //ha biztosra akarunk menni, hogy a konzol tudja a forintjelet, de a .NET 10 alapból tudja
        //CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("hu-HU");//ha biztosra akarunk menni, hogy a konzol a magyar formátumot használja, de a .NET 10 alapból tudja

        AlaposztalyKiprobalasa();
        LeszarmazottakKiprobalasa();
        HataresetekKiprobalasa();

        List<Bankszamla> szamlak = SzamlakLetrehozasa();
        Console.WriteLine("\n--- Polimorf működés: műveletek előtt ---");
        SzamlakKiirasa(szamlak);

        foreach (Bankszamla szamla in szamlak)
        {
            // A tényleges objektum típusa dönti el, melyik felülírt
            // metódus fut le, bár a változó típusa Bankszamla.
            szamla.Befizet(10000m);
            PenzfelvetelEredmeny eredmeny = szamla.Felvesz(15000m);
            Console.WriteLine($"{szamla.Tulajdonos}: {EredmenyUzenet(eredmeny)}");
        }

        Console.WriteLine("\n--- Műveletek után ---");
        SzamlakKiirasa(szamlak);
        LekerdezesekKiirasa(szamlak);

        // A menü friss példányokon indul, a bemutató nem változtatja meg őket.
        MenuFuttatasa(SzamlakLetrehozasa());
    }


    private static List<Bankszamla> SzamlakLetrehozasa()
    {
        // Kollekcióinicializáló: a lista létrehozása és feltöltése együtt.
        return new List<Bankszamla>
        {
            new Bankszamla("Kiss Anna", 50000m, 30000m),
            new Diakszamla("Tóth Éva", 30000m, "Technikum"),
            new PremiumSzamla("Nagy Béla", 80000m, 60000m, 2m),
            new UzletiSzamla("Varga Dániel", 100000m, 80000m, "Minta Kft.", 500m)
        };
    }
    private static void SzamlakKiirasa(List<Bankszamla> szamlak)
    {
        foreach (Bankszamla szamla in szamlak)
            Console.WriteLine(szamla); // A megfelelő ToString() fut le.
    }
    private static void FelvetelBemutatasa(Bankszamla szamla, decimal osszeg)
    {
        decimal eredetiEgyenleg = szamla.Egyenleg;
        PenzfelvetelEredmeny eredmeny = szamla.Felvesz(osszeg);
        Console.WriteLine($"{szamla.Tulajdonos}, felvétel: {osszeg:N2} Ft | " +
            $"{EredmenyUzenet(eredmeny)} | " +
            $"Egyenleg: {eredetiEgyenleg:N2} → {szamla.Egyenleg:N2} Ft");
    }
    private static string EredmenyUzenet(PenzfelvetelEredmeny eredmeny)
    {
        return eredmeny switch
        {
            PenzfelvetelEredmeny.Sikeres => "Sikeres pénzfelvétel.",
            PenzfelvetelEredmeny.ErvenytelenOsszeg => "Az összegnek pozitívnak kell lennie.",
            PenzfelvetelEredmeny.LimitTullepve => "Az összeg meghaladja a felvételi limitet.",
            PenzfelvetelEredmeny.NincsElegFedezet => "Nincs elegendő fedezet a teljes terhelésre.",
            _ => throw new ArgumentOutOfRangeException(nameof(eredmeny))
        };
    }

    private static void AlaposztalyKiprobalasa()
    {
        Console.WriteLine("--- Alaposztály kipróbálása ---");
        Bankszamla szamla = new Bankszamla("Kiss Anna", 50000m, 30000m);
        Console.WriteLine(szamla);
        szamla.Befizet(10000m);
        Console.WriteLine($"Befizetés után: {szamla.Egyenleg:N2} Ft");
        FelvetelBemutatasa(szamla, 20000m);
        FelvetelBemutatasa(szamla, 35000m);
        FelvetelBemutatasa(szamla, 0m);
        szamla.FelveteliLimit = 100000m;
        FelvetelBemutatasa(szamla, 50000m);

        try
        {
            szamla.Befizet(-1000m);
        }
        catch (ArgumentOutOfRangeException hiba)
        {
            Console.WriteLine($"Hibás befizetés: {hiba.Message}");
        }

        try
        {
            szamla.FelveteliLimit = 0m;
        }
        catch (ArgumentOutOfRangeException hiba)
        {
            Console.WriteLine($"Hibás limit: {hiba.Message}");
        }

        Console.WriteLine($"A hibás műveletek után: {szamla}");
        // szamla.egyenleg = -1000m; // Nem fordulna le: private mező.
        // szamla.Tulajdonos = "Más név"; // Csak olvasható tulajdonság.
        // szamla.Jovair(1000m); // Nem fordulna le: protected metódus.
    }

    private static void LeszarmazottakKiprobalasa()
    {
        Console.WriteLine("\n--- Leszármazottak kipróbálása ---");
        PremiumSzamla premium = new PremiumSzamla("Nagy Béla", 10000m, 50000m, 2m);
        premium.Befizet(5000m);
        Console.WriteLine($"Prémium befizetés után (elvárt: 15 100 Ft): {premium}");
        FelvetelBemutatasa(new Diakszamla("Tóth Éva", 30000m, "Technikum"), 25000m);
        FelvetelBemutatasa(new UzletiSzamla("Varga Dániel", 10000m,
            20000m, "Minta Kft.", 500m), 9500m);
        FelvetelBemutatasa(new UzletiSzamla("Varga Dániel", 10000m,
            20000m, "Minta Kft.", 500m), 10000m);
    }

    private static void HataresetekKiprobalasa()
    {
        Console.WriteLine("\n--- Határesetek ---");
        // Pontosan elegendő egyenleg és a limittel egyező összeg.
        FelvetelBemutatasa(new Bankszamla("Határeset", 20000m, 20000m), 20000m);
        FelvetelBemutatasa(new Bankszamla("Negatív összeg", 20000m, 20000m), -1m);
        PremiumSzamla premium = new PremiumSzamla("Nulla bónusz", 10000m, 20000m, 0m);
        premium.Befizet(5000m);
        Console.WriteLine($"Nulla bónusz, elvárt egyenleg 15 000 Ft: {premium}");
        FelvetelBemutatasa(new UzletiSzamla("Nulla díj", 10000m,
            20000m, "Teszt Kft.", 0m), 10000m);
    }



    private static void LekerdezesekKiirasa(List<Bankszamla> szamlak)
    {
        Console.WriteLine($"\nÖsszes egyenleg: {szamlak.Sum(s => s.Egyenleg):N2} Ft");
        Bankszamla? legtobbPenz = szamlak.OrderByDescending(s => s.Egyenleg).FirstOrDefault();
        Console.WriteLine(legtobbPenz is null
            ? "Nincs számla a listában."
            : $"Legnagyobb egyenlegű számla: {legtobbPenz}");

        Console.WriteLine("30 000 Ft feletti egyenlegű számlák:");
        foreach (Bankszamla szamla in szamlak.Where(s => s.Egyenleg > 30000m)
                     .OrderByDescending(s => s.Egyenleg))
            Console.WriteLine(szamla);
    }

//Bankszamla? → a referencia null is lehet.
//szamla?.Tulajdonos → ha szamla null, az eredmény null; különben lekéri a tulajdonost.
//nev ?? "Nincs név" → ha nev null, a helyettesítő szöveget adja.
    private static void MenuFuttatasa(List<Bankszamla> szamlak)
    {
        Bankszamla? kivalasztott = null;
        while (true)
        {
            Console.WriteLine("\n--- BANKSZÁMLAMENÜ ---");
            Console.WriteLine($"Kiválasztott számla: {kivalasztott?.Tulajdonos ?? "nincs"}");
            Console.WriteLine("1 – Számla kiválasztása");
            Console.WriteLine("2 – Befizetés");
            Console.WriteLine("3 – Pénzfelvétel");
            Console.WriteLine("4 – Kiválasztott számla adatai");
            Console.WriteLine("5 – Összes számla és LINQ-lekérdezések");
            Console.WriteLine("0 – Kilépés");
            Console.Write("Választás: ");
            string? valasztas = Console.ReadLine();

            // null: a bemenet véget ért; átirányított futtatásnál is kilépünk.
            if (valasztas is null || valasztas.Trim() == "0")
                return;

            switch (valasztas.Trim())
            {
                case "1":
                    kivalasztott = SzamlaKivalasztasa(szamlak, kivalasztott);
                    break;
                case "2":
                    if (kivalasztott is null)
                        Console.WriteLine("Előbb válassz számlát!");
                    else
                        BefizetesMenu(kivalasztott);
                    break;
                case "3":
                    if (kivalasztott is null)
                        Console.WriteLine("Előbb válassz számlát!");
                    else
                        PenzfelvetelMenu(kivalasztott);
                    break;
                case "4":
                    Console.WriteLine(kivalasztott is null
                        ? "Előbb válassz számlát!" : kivalasztott.ToString());
                    break;
                case "5":
                    SzamlakKiirasa(szamlak);
                    LekerdezesekKiirasa(szamlak);
                    break;
                default:
                    Console.WriteLine("Nincs ilyen menüpont.");
                    break;
            }
        }
    }

    private static Bankszamla? SzamlaKivalasztasa(
        List<Bankszamla> szamlak, Bankszamla? elozo)
    {
        for (int i = 0; i < szamlak.Count; i++)
            Console.WriteLine($"{i + 1}. {szamlak[i]}");

        Console.Write("A számla sorszáma: ");
        if (!int.TryParse(Console.ReadLine(), out int sorszam) ||
            sorszam < 1 || sorszam > szamlak.Count)
        {
            Console.WriteLine("Érvénytelen sorszám; a korábbi kiválasztás megmarad.");
            return elozo;
        }

        return szamlak[sorszam - 1];
    }

    private static bool OsszegBekerese(out decimal osszeg)
    {
        Console.Write("Összeg (Ft, tizedesvesszővel; pl. 1250,50): ");
        // Ez a bool egyértelmű: az összeg beolvasása sikerült-e?
        // Ezreselválasztót nem fogadunk el: a tizedespont ne legyen félreérthető.
        if (!decimal.TryParse(Console.ReadLine(), NumberStyles.Number &
                ~NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out osszeg)
            || osszeg <= 0)
        {
            Console.WriteLine("Pozitív számot adj meg!");
            return false;
        }

        return true;
    }

    private static void BefizetesMenu(Bankszamla szamla)
    {
        if (!OsszegBekerese(out decimal osszeg))
            return;

        try
        {
            szamla.Befizet(osszeg);
            Console.WriteLine($"Sikeres befizetés. Új egyenleg: {szamla.Egyenleg:N2} Ft");
        }
        catch (ArgumentOutOfRangeException hiba)
        {
            Console.WriteLine(hiba.Message);
        }
        catch (OverflowException)
        {
            Console.WriteLine("Az összeg túl nagy a számításhoz.");
        }
    }

    private static void PenzfelvetelMenu(Bankszamla szamla)
    {
        if (!OsszegBekerese(out decimal osszeg))
            return;

        try
        {
            Console.WriteLine(EredmenyUzenet(szamla.Felvesz(osszeg)));
            Console.WriteLine($"Egyenleg: {szamla.Egyenleg:N2} Ft");
        }
        catch (OverflowException)
        {
            Console.WriteLine("Az összeg túl nagy a számításhoz.");
        }
    }
}
