
using RimTalk.Util;
using Verse;

public class RimAgent : GameComponent
{
    public RimAgent(Game game)
    {
        int magicNumber = RustAgent.get_rust_magic_number();
        Logger.Message("RimAgent initialized with magic number: " + magicNumber);
    }
}