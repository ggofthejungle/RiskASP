﻿using it.unical.mat.embasp.languages;

namespace EmbASP.predicates
{
    [Id("turn")]
    public class Turn
    {
        [Param(0)] private int _turn;

        [Param(1)] private string _player;
    }
}