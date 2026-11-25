using ConsoleEngine;

namespace MyGame
{
    internal class Empty : GameBase
    {
        public Empty(int width, int height, int framePerSecond) : base(width, height, framePerSecond)
        {
            
        }
        protected override void Input(ConsoleKey key)
        {
            throw new NotImplementedException();
        }

        protected override void Render(Screen screen)
        {
            throw new NotImplementedException();
        }

        protected override void Update(float deltaTime)
        {
            throw new NotImplementedException();
        }
    }
}
