using System.Collections;
using Gridlock.Banker;
using Gridlock.Core;
using UnityEngine;

namespace Gridlock.Guide
{
    /// <summary>
    /// Narrated guided tour of Banker mode (about two minutes), told on Level 1's lot: fleets are processes and the two
    /// zones are resource types; parked cars are Allocation, street cars are Need, gate cars are the Request and empty
    /// bays are Available; how the safety check works; then the learner decides GRANT or DENY and the narrator explains
    /// the verdict while the fleets light up in safe-sequence order.
    /// </summary>
    public class BankerTour : NarratedTour
    {
        const string TourKey = "banker";

        public static bool Seen
        {
            get => IsSeen(TourKey);
            set => SetSeen(TourKey, value);
        }

        [SerializeField] BankerGame game;
        [SerializeField] BankerLotView lot;
        [Tooltip("Seconds of silence on the hands-on step before the hint, and again before the guide decides.")]
        [SerializeField] float decideWaitSeconds = 18f;

        protected override string Key => TourKey;
        protected override string HandsOnStep => "banker_turn";
        // The hint is an aside; "wrong" replaces "right", so only one of them is numbered.
        protected override bool Counted(string id) => id != "banker_hint" && id != "banker_wrong";

        public void Init(BankerGame bankerGame, BankerLotView lotView)
        {
            game = bankerGame;
            lot = lotView;
        }

        protected override void PrepareFirstRun() => game.Locked = true;

        protected override void OnBegin()
        {
            game.RestartTutorial(); // the tour is told on Level 1's lot
            game.Locked = true;
        }

        protected override void OnEnd(bool finished) => game.Locked = false;

        protected override IEnumerator Script()
        {
            var carRoof = new Vector3(0f, 2.4f, 0f);
            var bayTop = new Vector3(0f, 0.2f, 0f);
            var lotCentre = (lot.Bay(1).ParkingPoint + lot.Bay(4).ParkingPoint) * 0.5f + new Vector3(0f, 0f, 4f);

            yield return new WaitForSeconds(1.2f); // let the scene fade in

            Say("banker_welcome");
            Focus(lotCentre, 34f, 40f);
            yield return EndOfLine();

            Say("banker_fleets");
            Focus(lotCentre, 28f, 46f);
            marker.Point(lot.ParkedCar(1).transform, carRoof, "FLEET  =  PROCESS");
            yield return At(0.42f);
            marker.Point(lot.FreeBay(0).ParkingPoint + bayTop, "ZONE N  =  RESOURCE TYPE");
            yield return At(0.75f);
            marker.Point(lot.FreeBay(1).ParkingPoint + bayTop, "ZONE S");
            yield return EndOfLine();

            Say("banker_alloc");
            marker.Point(lot.ParkedCar(2).transform, carRoof, "ALLOCATION");
            yield return EndOfLine();

            Say("banker_need");
            var street = lot.StreetCar();
            Focus(street.transform.position + new Vector3(4f, 0f, 3f), 26f, 40f);
            marker.Point(street.transform, carRoof, "NEED");
            yield return EndOfLine();

            Say("banker_request");
            var gate = lot.GateCar();
            Focus(gate.transform.position + new Vector3(4f, 0f, 4f), 24f, 42f);
            marker.Point(gate.transform, carRoof, "REQUEST");
            yield return At(0.62f);
            marker.Point(lot.FreeBay(0).ParkingPoint + bayTop, "AVAILABLE");
            yield return EndOfLine();
            marker.Hide();

            Say("banker_safety");
            Focus(lotCentre, 32f, 44f);
            yield return EndOfLine();

            // Hands-on: the learner decides.
            Say("banker_turn");
            game.Locked = false;
            ShowActions(new GuideScreen.Action("GRANT", ViewColor.Safe, game.Grant),
                        new GuideScreen.Action("DENY", ViewColor.Deadlock, game.Deny));
            float deadline = Time.time + LineLength + decideWaitSeconds;
            yield return new WaitUntil(() => game.Verdict != null || Time.time > deadline);
            if (game.Verdict == null)
            {
                Say("banker_hint");
                deadline = Time.time + LineLength + decideWaitSeconds;
                yield return new WaitUntil(() => game.Verdict != null || Time.time > deadline);
                if (game.Verdict == null) game.Grant();
            }
            HideActions();
            game.Locked = true;

            Say(game.Verdict.Correct ? "banker_right" : "banker_wrong"); // the lot replays the safety check meanwhile
            Focus(lotCentre, 28f, 46f);
            yield return EndOfLine();

            Say("banker_outro");
            Focus(lotCentre, 34f, 40f);
            yield return EndOfLine();
        }
    }
}
