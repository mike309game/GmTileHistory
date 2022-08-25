namespace GmTileHistory {
	internal class Program {
		static void Main(string[] args) {
			using(var game = new GameMain(args)) {
				game.Run();
			}
		}
	}
}