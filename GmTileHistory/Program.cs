namespace GmTileHistory {
	internal class Program {
		static void Main(string[] args) {
			string? wadPath = null;
			foreach(var arg in args) {
				if (!arg.StartsWith("/")) {
					wadPath = arg;
					break;
				}
			}
			using(var game = new GameMain(wadPath)) {
				game.Run();
			}
		}
	}
}