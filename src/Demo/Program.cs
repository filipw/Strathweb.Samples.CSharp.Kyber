﻿using System.Text;
using Org.BouncyCastle.Pqc.Crypto.Crystals.Dilithium;
using Org.BouncyCastle.Pqc.Crypto.Crystals.Kyber;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;
using Spectre.Console;

var demo = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Choose the [green]demo[/] to run?")
        .AddChoices(new[]
        {
            "Kyber", "Dilithium"
        }));

switch (demo)
{
    case "Kyber":
        RunKyber();
        break;
    case "Dilithium":
        RunDilithium();
        break;
    default:
        Console.WriteLine("Nothing selected!");
        break;
}

static void RunDilithium()
{
    Console.WriteLine("***************** DILITHIUM *******************");
    
    var raw = "Hello, Dilithium!";
    Console.WriteLine($"Raw Message: {raw}");

    var data = Hex.Encode(Encoding.ASCII.GetBytes(raw));
    PrintPanel("Message", new[] { $"Raw: {raw}", $"Encoded: {data.PrettyPrint()}" });

    var random = new SecureRandom();
    var keyGenParameters = new DilithiumKeyGenerationParameters(random, DilithiumParameters.Dilithium3);
    var dilithiumKeyPairGenerator = new DilithiumKeyPairGenerator();
    dilithiumKeyPairGenerator.Init(keyGenParameters);

    var keyPair = dilithiumKeyPairGenerator.GenerateKeyPair();

    // get and view the keys
    var publicKey = (DilithiumPublicKeyParameters)keyPair.Public;
    var privateKey = (DilithiumPrivateKeyParameters)keyPair.Private;
    var pubEncoded = publicKey.GetEncoded();
    var privateEncoded = privateKey.GetEncoded();
    PrintPanel("Keys", new[] { $":unlocked: Public: {pubEncoded.PrettyPrint()}", $":locked: Private: {privateEncoded.PrettyPrint()}" });

    // sign
    var alice = new DilithiumSigner();
    alice.Init(true, privateKey);
    var signature = alice.GenerateSignature(data);
    PrintPanel("Signature", new[] { $":pen: {signature.PrettyPrint()}" });

    // verify signature
    var bob = new DilithiumSigner();
    bob.Init(false, publicKey);
    var verified = bob.VerifySignature(data, signature);
    PrintPanel("Verification", new[] { $"{(verified ? ":check_mark_button:" : ":cross_mark:")} Verified!" });

    var aliceRecovered = new DilithiumSigner();
    var recoveredKey = RecoverPrivateKeyFromExport(privateKey.GetEncoded(), DilithiumParameters.Dilithium3);
    aliceRecovered.Init(true, recoveredKey);
    var signature2 = aliceRecovered.GenerateSignature(data);
    PrintPanel("Signature (from key loaded from JWK)", new[] { $":pen: {signature2.PrettyPrint()}" });
    
    // verify signature
    var bobReVerified = bob.VerifySignature(data, signature2);
    PrintPanel("Reverification", new[] { $"{(bobReVerified ? ":check_mark_button:" : ":cross_mark:")} Verified!" });
}

static DilithiumPrivateKeyParameters RecoverPrivateKeyFromExport(byte[] encodedPrivateKey, DilithiumParameters dilithiumParameters)
{
    const int seedBytes = 32;
    int s1Length;
    int s2Length;
    int t0Length;

    if (dilithiumParameters == DilithiumParameters.Dilithium2)
    {
        s1Length = 4 * 96; 
        s2Length = 4 * 96;
        t0Length = 4 * 416;
    } 
    else if (dilithiumParameters == DilithiumParameters.Dilithium3)
    {
        s1Length = 5 * 128;
        s2Length = 6 * 128;
        t0Length = 6 * 416;
    } 
    else if (dilithiumParameters == DilithiumParameters.Dilithium5)
    {
        s1Length = 7 * 96;
        s2Length = 8 * 96;
        t0Length = 8 * 416;
    }
    else
    {
        throw new NotSupportedException("Unsupported mode");
    }
    
    var rho = new byte[seedBytes]; // SeedBytes length
    var k = new byte[seedBytes]; // SeedBytes length
    var tr = new byte[seedBytes]; // SeedBytes length
    var s1 = new byte[s1Length]; // L * PolyEtaPackedBytes
    var s2 = new byte[s2Length]; // K * PolyEtaPackedBytes
    var t0 = new byte[t0Length]; // K * PolyT0PackedBytes

    var offset = 0;
    Array.Copy(encodedPrivateKey, offset, rho, 0, seedBytes);
    offset += seedBytes;
    Array.Copy(encodedPrivateKey, offset, k, 0, seedBytes);
    offset += seedBytes;
    Array.Copy(encodedPrivateKey, offset, tr, 0, seedBytes);
    offset += seedBytes;
    Array.Copy(encodedPrivateKey, offset, s1, 0, s1Length);
    offset += s1Length;
    Array.Copy(encodedPrivateKey, offset, s2, 0, s2Length);
    offset += s2Length;
    Array.Copy(encodedPrivateKey, offset, t0, 0, t0Length);
    offset += t0Length;
    
    // Take all remaining bytes as t1
    var remainingLength = encodedPrivateKey.Length - offset;
    var t1 = new byte[remainingLength];
    Array.Copy(encodedPrivateKey, offset, t1, 0, remainingLength);

    return new DilithiumPrivateKeyParameters(dilithiumParameters, rho, k, tr, s1, s2, t0, t1);
}

static void RunKyber() 
{
    Console.WriteLine("***************** KYBER *******************");
    
    var random = new SecureRandom();
    var keyGenParameters = new KyberKeyGenerationParameters(random, KyberParameters.kyber768);
    
    var kyberKeyPairGenerator = new KyberKeyPairGenerator();
    kyberKeyPairGenerator.Init(keyGenParameters);

    // generate key pair for Alice
    var aliceKeyPair = kyberKeyPairGenerator.GenerateKeyPair();

    // get and view the keys
    var alicePublic = (KyberPublicKeyParameters)aliceKeyPair.Public;
    var alicePrivate = (KyberPrivateKeyParameters)aliceKeyPair.Private;
    var pubEncoded = alicePublic.GetEncoded();
    var privateEncoded = alicePrivate.GetEncoded();
    PrintPanel("Alice's keys", new[] { $":unlocked: Public: {pubEncoded.PrettyPrint()}", $":locked: Private: {privateEncoded.PrettyPrint()}" });

    // Bob encapsulates a new shared secret using Alice's public key
    var bobKyberKemGenerator = new KyberKemGenerator(random);
    var encapsulatedSecret = bobKyberKemGenerator.GenerateEncapsulated(alicePublic);
    var bobSecret = encapsulatedSecret.GetSecret();

    // cipher text produced by Bob and sent to Alice
    var cipherText = encapsulatedSecret.GetEncapsulation();

    // Alice decapsulates a new shared secret using Alice's private key
    var aliceKemExtractor = new KyberKemExtractor(alicePrivate);
    var aliceSecret = aliceKemExtractor.ExtractSecret(cipherText);
    PrintPanel("Key encapsulation", new[] { $":man: Bob's secret: {bobSecret.PrettyPrint()}", $":locked_with_key: Cipher text (Bob -> Alice): {cipherText.PrettyPrint()}", $":woman: Alice's secret: {aliceSecret.PrettyPrint()}" });

    // Compare secrets
    var equal = bobSecret.SequenceEqual(aliceSecret);
    PrintPanel("Verification", new[] { $"{(equal ? ":check_mark_button:" : ":cross_mark:")} Secrets equal!" });
}

static void PrintPanel(string header, string[] data)
{
    var content = string.Join(Environment.NewLine, data);
    var panel = new Panel(content)
    {
        Header = new PanelHeader(header)
    };
    AnsiConsole.Write(panel);
}

public static class FormatExtensions
{
    public static string PrettyPrint(this byte[] bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        return base64.Length > 50 ? $"{base64[..25]}...{base64[^25..]}" : base64;
    }
}