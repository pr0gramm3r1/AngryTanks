using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

using log4net;

using AngryTanks.Common;
using AngryTanks.Common.Protocol;

namespace AngryTanks.Client
{
    public enum ShotState
    {
        /// <summary>
        /// The final state of the shot.
        /// The shot can be removed at this state.
        /// </summary>
        None,

        /// <summary>
        /// The starting state of the shot.
        /// </summary>
        Starting,

        /// <summary>
        /// The active phase of the shot.
        /// </summary>
        Active,

        /// <summary>
        /// The ending phase of the shot.
        /// </summary>
        Ending,

        /// <summary>
        /// The ended phase of the shot.
        /// A shot will remain in Ended until it has reloaded, where it will go to None.
        /// </summary>
        Ended
    }

    public class Shot : DynamicSprite
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private ShotState state = ShotState.None;

        public ShotState State
        {
            get { return state; }
        }

        private readonly Byte slot;

        public Byte Slot
        {
            get { return slot; }
        }

        /// <summary>
        /// <see cref="Player"/> the <see cref="Shot"/> is associated with.
        /// </summary>
        private Player player;

        /// <summary>
        /// Stores the position the shot originated from.
        /// </summary>
        private Vector2 initialPosition;

        /// <summary>
        /// Maximum distance the shot will travel.
        /// </summary>
        private Single maxShotRange;

        /// <summary>
        /// Time the shot was created.
        /// </summary>
        private TimeSpan initialTime = TimeSpan.Zero;

        /// <summary>
        /// The maximum time to live for the shot, which is also the reload time.
        /// </summary>
        private TimeSpan maxTTL;

        public Shot(World world, Player player, Byte slot, Vector2 initialPosition, Single rotation, Vector2 initialVelocity)
            : base(world, GetTexture(world), initialPosition, new Vector2(2, 2), rotation)
        {
            Single shotSpeed = (Single)World.VarDB["shotSpeed"].Value;

            // get the unit vector in the forward direction
            Velocity = new Vector2((Single)Math.Cos(Rotation - Math.PI / 2),
                                   (Single)Math.Sin(Rotation - Math.PI / 2));

            // get our shot velocity by multiplying by the magnitude
            Velocity *= shotSpeed;

            // add the velocity from the tank to the shot
            //Velocity += initialVelocity;

            // store info...
            this.initialPosition = initialPosition;
            this.slot = slot;
            this.player = player;
            this.maxShotRange = (Single)World.VarDB["shotRange"].Value;
            this.maxTTL = new TimeSpan(0, 0, 0, 0, (int)((Single)World.VarDB["reloadTime"].Value * 1000));

            // start the shot
            state = ShotState.Starting;
        }

        public static Byte AllocateSlot(Dictionary<Byte, Shot> shots)
        {
            Byte earliestSlot = ProtocolInformation.DummyShot;

            for (Byte i = 0; i < ProtocolInformation.MaxShots; ++i)
            {
                // we didn't find a shot at this slot
                if (!shots.ContainsKey(i))
                {
                    // we didn't find a shot at i, so let's save this slot in case we make it out of this loop
                    if (i < earliestSlot)
                        earliestSlot = i;
                }
            }

            // we didn't find an open slot since it's still at dummy slot...
            if (earliestSlot == ProtocolInformation.DummyShot)
                return ProtocolInformation.DummyShot;

            return earliestSlot;
        }

        protected static Texture2D GetTexture(World world)
        {
            // TODO get the correct texture depending on team
            return world.Content.Load<Texture2D>("textures/bz/rogue_bolt");
        }

        public void End()
        {
            // we are no longer moving now
            Velocity = Vector2.Zero;

            // and we are ending
            state = ShotState.Ending;
        }

        public override void Update(GameTime gameTime)
        {
            // see if we need to setup initial time
            if (initialTime == TimeSpan.Zero)
                initialTime = gameTime.TotalRealTime;

            // if we're starting, move straight to active
            if (State == ShotState.Starting)
                state = ShotState.Active;

            // if we're ending, move straight to ended
            // TODO logic to handle explosions while ending
            if (State == ShotState.Ending)
                state = ShotState.Ended;

            // can the shot move to the none state yet?
            if (initialTime + maxTTL <= gameTime.TotalRealTime)
            {
                state = ShotState.None;
                Log.DebugFormat("putting shot {0} in state none, initial time: {1}, max ttl: {2}, total real time: {3}",
                                Slot, initialTime, maxTTL, gameTime.TotalRealTime);
            }

            // see if we can bail out now
            if (State == ShotState.Ended || State == ShotState.None)
                return;

            // see if we collide with any world objects
            Single overlap;
            Vector2 collisionProjection;

            if (FindNearestCollision(World.MapGrid.PotentialIntersects(this), out overlap, out collisionProjection))
            {
                // move our position back
                Position += overlap * collisionProjection;

                // end shot
                End();
            }

            Position += Velocity * (Single)gameTime.ElapsedGameTime.TotalSeconds;

            // the shot has travelled its maximum distance
            if (Math.Abs(Vector2.Distance(initialPosition, Position)) >= maxShotRange)
            {
                // end shot
                End();
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            // we have nothing to draw
            if (State == ShotState.Ended || State == ShotState.None)
                return;

            DrawStretched(gameTime, spriteBatch);
        }
    }
}
