# Pinned build-time payloads. Every value was verified on 2026-10-31 against an independent source
# (GitHub release digest + Authenticode publisher for executables). Update all fields together and
# re-verify when bumping a version; never fetch any of these during installation.
@{
    PawnIO = @{
        Version  = '2.2.0'
        File     = 'PawnIO_setup.exe'
        Url      = 'https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe'
        Sha256   = '1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032'
        # Official signed production edition. Never pass -unrestricted (unsigned developer edition).
        Signer   = 'CN=namazso.eu, O=namazso, L=Debrecen, C=HU'
    }
    PawnIOSource = @{
        File   = 'PawnIO-2.2.0-source.zip'
        Url    = 'https://codeload.github.com/namazso/PawnIO/zip/5cdf470831fdfff3f7f1d06363ca6b230f3bf35a'
        Sha256 = '638D685555DB311EF9F946284DC4CD5720D11C6CB9C5EC5140EBB7F60373440F'
    }
    PawnPPSource = @{
        # PawnIO submodule PawnPP at the commit referenced by PawnIO 2.2.0 (needed for complete corresponding source).
        File   = 'PawnPP-e64e4c3-source.zip'
        Url    = 'https://codeload.github.com/namazso/PawnPP/zip/e64e4c37b2d8ba0d8ee57205faf8183aee12c438'
        Sha256 = '20AA0638B95C90D296935310F3F28CC22DE1FB24855C5BAA6721E2AE95EB6042'
    }
    InnoSetup = @{
        Version = '6.7.4'
        File    = 'innosetup-6.7.4.exe'
        Url     = 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_4/innosetup-6.7.4.exe'
        Sha256  = 'A549929319F9120716677C685535CA4671E4B937B5FDC87E2585F32D8EB30E8C'
        Signer  = 'CN=Pyrsys B.V., O=Pyrsys B.V., S=Noord-Holland, C=NL'
    }
}
