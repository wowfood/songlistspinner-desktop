window.SonglistSpinnerContracts = Object.freeze({
    backgroundModes: Object.freeze({
        color: 'color',
        transparent: 'transparent',
        legacyTransparent: 'transparant'
    }),
    playedListPositions: Object.freeze({
        left: 'left',
        right: 'right',
        default: 'right'
    }),
    nowPlayingPositions: Object.freeze({
        values: Object.freeze([
            'top-left', 'top-center', 'top-right',
            'bottom-left', 'bottom-center', 'bottom-right'
        ]),
        default: 'bottom-left'
    }),
    overlayEvents: Object.freeze({
        initialState: 'init_state',
        updateSongs: 'update_songs',
        spinCommand: 'spin_command',
        winnerReveal: 'winner_reveal',
        closeWinner: 'close_winner',
        setWheelVisible: 'set_wheel_visible',
        setCollapse: 'set_collapse',
        setPlayedListWidth: 'set_played_list_width'
    }),
    messageTypes: Object.freeze({
        settingsPreview: 'songlistspinner-settings-preview'
    })
})
