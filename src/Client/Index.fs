module Index

open Elmish
open SAFE
open Shared

type Model = {
    StartDate: string
    EndDate: string
    NewResponse: RemoteData<Response>
    NewPowerSystemResponse: RemoteData<PowerSystemResponse>
}

type Msg =
    | SetStartDate of string
    | SetEndDate of string
    | LoadNewResponse of ApiCall<string * string, Response>
    | LoadPowerSystemResponse of ApiCall<string * string, PowerSystemResponse>


let eleringApi = Api.makeProxy<IEleringApi> ()

let init () =
    let initialModel = {
        StartDate = ""
        EndDate = ""
        NewResponse = NotStarted
        NewPowerSystemResponse = NotStarted
    }

    initialModel, Cmd.none

let update msg model =
    match msg with
    | SetStartDate value -> { model with StartDate = value }, Cmd.none
    | SetEndDate value -> { model with EndDate = value }, Cmd.none
    | LoadNewResponse msg ->
        match msg with
        | Start (startDate, endDate) ->
            let loadNewResponseCmd =
                let newRequest = Request.create (startDate, endDate)
                Cmd.OfAsync.perform (fun () -> eleringApi.getDayAheadPriceData newRequest) () (Finished >> LoadNewResponse)

            {
                model with
                    NewResponse = model.NewResponse.StartLoading()
                    StartDate = startDate
                    EndDate = endDate
            }, loadNewResponseCmd
        | Finished response ->
            {
                model with
                    NewResponse = RemoteData.Loaded response
            },
            Cmd.none
    | LoadPowerSystemResponse msg ->
        match msg with
        | Start (startDate, endDate) ->
            let loadPowerSystemResponseCmd =
                let newRequest = Request.create (startDate, endDate)
                Cmd.OfAsync.perform (fun () -> eleringApi.getPowerSystemData newRequest) () (Finished >> LoadPowerSystemResponse)

            {
                model with
                    NewPowerSystemResponse = model.NewPowerSystemResponse.StartLoading()
                    StartDate = startDate
                    EndDate = endDate
            }, loadPowerSystemResponseCmd
        | Finished response ->
            {
                model with
                    NewPowerSystemResponse = RemoteData.Loaded response
            },
            Cmd.none

open Feliz
open Feliz.Recharts
open Browser
open System

let transformResponseData (response: Response) =
    let transformPriceData (priceData: PriceData list) =
        priceData |> List.map (fun pd ->
            let timestampInMilliseconds = int64 pd.timestamp * 1000L
            {| label = "EE"; timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampInMilliseconds).ToString("yyyy-MM-dd HH:mm:ss"); price = pd.price |})
        |> Seq.ofList

    transformPriceData response.data.ee

let createGradient (id: string) color =
    Svg.linearGradient [
        svg.id id
        svg.x1 0; svg.x2 0
        svg.y1 0; svg.y2 1
        svg.children [
            Svg.stop [
                svg.offset(length.percent 5)
                svg.stopColor color
                svg.stopOpacity 0.8
            ]
            Svg.stop [
                svg.offset(length.percent 95)
                svg.stopColor color
                svg.stopOpacity 0.0
            ]
        ]
    ]

[<ReactComponent>]
let EleringChart(response: Response) =
    let data = transformResponseData response

    Recharts.areaChart [
        areaChart.width 1000
        areaChart.height 250
        areaChart.data data
        areaChart.margin(top=10, right=30)
        areaChart.children [
            Svg.defs [
                createGradient "colorPrice" "#8884d8"
            ]
            Recharts.xAxis [ xAxis.dataKey (fun point -> string point.timestamp) ]
            Recharts.yAxis []
            Recharts.tooltip [ ]
            Recharts.cartesianGrid [ cartesianGrid.strokeDasharray(3, 3) ]

            Recharts.area [
                area.monotone
                area.dataKey (fun point -> point.price)
                area.stroke "#8884d8"
                area.fillOpacity 1
                area.fill "url(#colorPrice)"
            ]
        ]
    ]

let transformPowerSystemData (response: PowerSystemResponse) =
    response.data
    |> List.map (fun psd ->
        {|
            label = "renewable"
            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(int64 psd.timestamp * 1000L).ToString("yyyy-MM-dd HH:mm:ss")
            production_renewable = defaultArg psd.production_renewable 0
        |})
    |> Seq.ofList

[<ReactComponent>]
let PowerSystemChart(response: PowerSystemResponse) =
    let data = transformPowerSystemData response

    Recharts.areaChart [
        areaChart.width 1000
        areaChart.height 250
        areaChart.data data
        areaChart.margin(top=10, right=30)
        areaChart.children [
            Svg.defs [
                createGradient "colorSolar" "#82ca9d"
            ]
            Recharts.xAxis [ xAxis.dataKey (fun point -> string point.timestamp)]
            Recharts.yAxis []
            Recharts.tooltip []
            Recharts.cartesianGrid [ cartesianGrid.strokeDasharray(3, 3) ]
            Recharts.area [
                area.monotone
                area.dataKey (fun point -> point.production_renewable)
                area.stroke "#82ca9d"
                area.fillOpacity 1
                area.fill "url(#colorSolar)"
            ]
        ]
    ]

module ViewComponents =
    let eleringAction model dispatch =
        Html.div [
            prop.className "flex flex-col sm:flex-row mt-4 gap-4"
            prop.children [
                Html.input [
                    prop.className
                        "shadow appearance-none border rounded w-full py-2 px-3 outline-none focus:ring-2 ring-teal-300 text-grey-darker"
                    prop.value model.StartDate
                    prop.type'.date
                    prop.placeholder "Add start date?"
                    prop.autoFocus true
                    prop.onChange (SetStartDate >> dispatch)
                ]
                Html.input [
                    prop.className
                        "shadow appearance-none border rounded w-full py-2 px-3 outline-none focus:ring-2 ring-teal-300 text-grey-darker"
                    prop.value model.EndDate
                    prop.type'.date
                    prop.placeholder "Add end date?"
                    prop.autoFocus true
                    prop.onChange (SetEndDate >> dispatch)
                ]
                Html.button [
                    prop.className
                        "flex-no-shrink p-2 px-12 rounded bg-teal-600 outline-none focus:ring-2 ring-teal-300 font-bold text-white hover:bg-teal disabled:opacity-30 disabled:cursor-not-allowed"
                    prop.disabled (Request.isValid (model.StartDate, model.EndDate) |> not)
                    prop.onClick (fun _ ->
                        async {
                            dispatch (LoadNewResponse(Start (model.StartDate, model.EndDate)))
                            do! Async.Sleep 3000
                            dispatch (LoadPowerSystemResponse(Start (model.StartDate, model.EndDate)))
                        } |> Async.StartImmediate
                    )
                    prop.text "Confirm"
                ]
            ]
        ]

    let eleringDashboard model dispatch =
        Html.div [
            prop.className "bg-white/80 rounded-md shadow-md p-4 w-full lg:w-full lg:max-w-5xl"
            prop.children [
                eleringAction model dispatch
                Html.div [
                    prop.children [
                        Html.h2 [
                            prop.text "NordPool day-ahead price data"
                        ]
                        match model.NewResponse with
                        | NotStarted -> Html.text "Not Started."
                        | Loading None -> Html.text "Loading..."
                        | Loading(Some response)
                        | Loaded response -> EleringChart response
                        Html.h2 [
                            prop.text "Renewable Power system data"
                        ]
                        match model.NewPowerSystemResponse with
                        | NotStarted -> Html.text "Not Started."
                        | Loading None -> Html.text "Loading..."
                        | Loading(Some response)
                        | Loaded response ->  PowerSystemChart response

                    ]
                ]
            ]
        ]


let view model dispatch =
    Html.section [
        prop.className "h-screen w-screen"
        prop.style [
            style.backgroundSize "cover"
            style.backgroundImageUrl "https://unsplash.it/1200/900?electricity"
            style.backgroundPosition "no-repeat center center fixed"
        ]
        prop.children [
            Html.a [
                prop.href "https://elering.ee/"
                prop.className "absolute block ml-12 h-12 w-120 bg-teal-300 hover:cursor-pointer hover:bg-teal-400"
                prop.children [ Html.img [ prop.src "data:image/svg+xml,%3Csvg width='160' height='40' viewBox='0 0 160 40' fill='none' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M18.3758 37.6895C8.24339 37.6895 0 29.2357 0 18.8446C0 17.7831 0.839075 16.9226 1.8741 16.9226C2.90952 16.9226 3.74821 17.7831 3.74821 18.8446C3.74821 27.1164 10.3099 33.8456 18.3758 33.8456C19.4108 33.8456 20.2499 34.7057 20.2499 35.7676C20.2499 36.829 19.4108 37.6895 18.3758 37.6895Z' fill='white'/%3E%3Cpath d='M34.875 20.7665C33.84 20.7665 33.0013 19.906 33.0013 18.8445C33.0013 10.5731 26.4392 3.8439 18.3741 3.8439C17.3387 3.8439 16.5 2.9834 16.5 1.92195C16.5 0.860498 17.3387 2.59311e-06 18.3741 2.59311e-06C28.5061 2.59311e-06 36.7491 8.45385 36.7491 18.8445C36.7491 19.906 35.9104 20.7665 34.875 20.7665Z' fill='white'/%3E%3Cpath d='M13.881 16.3665C14.0781 15.3022 14.4583 14.4288 14.9979 13.8119C15.7418 12.9699 16.7603 12.5587 18.1077 12.5547C19.5217 12.5587 20.6433 12.9952 21.4377 13.8493C22.0217 14.4835 22.4206 15.3275 22.628 16.3665H13.881ZM25.3231 10.9089C23.6685 8.8953 21.2473 7.83103 18.3193 7.83103C18.304 7.83103 18.2884 7.83144 18.2731 7.83103C15.3181 7.83103 12.8922 8.80487 11.2571 10.6464C9.57038 12.5415 8.71484 15.2994 8.71484 18.8435C8.71484 22.5198 9.65542 25.3428 11.5115 27.2346C13.2218 28.9753 15.6144 29.8583 18.6325 29.8583C21.2359 29.8583 23.6332 28.9922 25.956 27.2137C26.5329 26.7792 26.6674 25.9348 26.2531 25.3271L25.1711 23.7597C24.9633 23.4558 24.6541 23.2561 24.3018 23.1978C23.9463 23.1391 23.5768 23.2307 23.2891 23.4498C22.2141 24.2621 20.2471 25.1346 18.4632 25.1346H18.4494C17.0668 25.1302 16.0141 24.7383 15.2338 23.9377C14.5786 23.2617 14.131 22.2947 13.8982 21.0548H26.2468C26.9758 21.0548 27.5617 20.4676 27.5789 19.7498C27.5805 19.7249 27.617 19.1236 27.617 18.1297C27.617 15.268 26.8022 12.703 25.3231 10.9089' fill='white'/%3E%3Cpath d='M129.535 8.43422C128.013 8.43422 125.675 8.78228 123.356 10.3196C123.014 9.64197 121.879 8.82126 120.732 8.429C120.482 8.34339 120.208 8.45512 120.079 8.68863L119.112 10.4755C118.965 10.748 119.061 11.0832 119.325 11.2416C119.93 11.5949 120.55 11.8312 120.55 13.0281V28.5295C120.55 28.841 120.791 29.093 121.1 29.093H123.424C123.723 29.093 123.969 28.8386 123.969 28.5295C123.969 28.5295 123.966 15.6305 123.969 13.641C125.759 12.0776 127.845 11.7271 129.264 11.7271C130.398 11.7271 131.487 12.0466 132.016 12.7685C132.667 13.6695 132.745 14.3552 132.745 17.417V28.5295C132.745 28.6774 132.801 28.8229 132.903 28.9266C133.007 29.0359 133.146 29.093 133.29 29.093H135.615C135.757 29.093 135.901 29.0359 136 28.9266C136.103 28.8229 136.164 28.6774 136.164 28.5295V17.0585C136.164 12.1712 135.326 11.3011 134.293 10.187C133.31 9.12511 131.56 8.43422 129.535 8.43422Z' fill='white'/%3E%3Cpath d='M113.21 5.79834C114.367 5.79593 115.304 4.83495 115.307 3.6481C115.304 2.46366 114.367 1.50028 113.21 1.49787C112.053 1.50028 111.116 2.46366 111.113 3.6481C111.116 4.83495 112.053 5.79593 113.21 5.79834Z' fill='white'/%3E%3Cpath d='M108.478 8.63156C108.169 8.52505 107.617 8.43945 106.781 8.43422C105.26 8.43422 102.919 8.78228 100.6 10.3196C100.258 9.64197 99.1262 8.82126 97.9791 8.429C97.7283 8.34339 97.4524 8.45512 97.3281 8.68863L96.3586 10.4755C96.2116 10.748 96.3053 11.0832 96.5686 11.2416C97.1737 11.5973 97.7992 11.8312 97.7992 13.0281V28.5295C97.7992 28.841 98.0375 29.093 98.3463 29.093H100.671C100.972 29.093 101.215 28.8382 101.215 28.5295C101.215 28.5295 101.21 15.6305 101.215 13.6438C103.006 12.0776 105.092 11.7271 106.51 11.7271C107.151 11.7271 107.873 11.836 108.161 11.8907C108.587 11.9686 108.842 11.7219 108.842 11.3429V9.16128C108.842 8.92255 108.696 8.71194 108.478 8.63156Z' fill='white'/%3E%3Cpath d='M116.814 26.9686C115.746 26.3585 114.885 26.2857 114.885 23.4888V9.47813C114.885 9.16665 114.644 8.91747 114.338 8.91747H112.016C111.714 8.91747 111.469 9.16906 111.469 9.47813V22.894C111.469 25.4494 111.816 26.7399 112.876 28.0228C113.53 28.8126 114.394 29.3785 115.358 29.737C115.586 29.8202 115.847 29.7579 116.009 29.4902L117.017 27.7451C117.159 27.4855 117.085 27.1245 116.814 26.9686Z' fill='white'/%3E%3Cpath d='M74.3144 26.9686C73.2456 26.3585 72.3874 26.2858 72.3874 23.4888V1.01224C72.3874 0.703165 72.1416 0.451166 71.8379 0.451166H69.5159C69.2145 0.451166 68.9688 0.705576 68.9688 1.01224V22.894C68.9688 25.4494 69.3156 26.7399 70.3765 28.0228C71.0302 28.8126 71.8908 29.3785 72.8584 29.737C73.081 29.8202 73.3444 29.7579 73.5117 29.4902L74.517 27.7451C74.6561 27.4855 74.5852 27.1245 74.3144 26.9686Z' fill='white'/%3E%3Cpath d='M148.987 36.9382C144.854 36.9382 141.506 35.5982 141.506 32.9701C141.506 30.3886 144.854 29.0045 148.987 29.0045C153.114 29.0045 156.462 30.3886 156.462 32.9701C156.462 35.5982 153.114 36.9382 148.987 36.9382ZM145.008 12.7479C146.024 12.0517 147.429 11.735 148.761 11.7375C150.258 11.735 151.676 12.057 152.668 12.7479C153.658 13.4436 154.279 14.4488 154.289 16.0978C154.279 17.7207 153.653 18.7235 152.643 19.4297C151.628 20.1258 150.179 20.4582 148.647 20.4554C147.333 20.4582 145.958 20.1491 144.968 19.4582C143.975 18.7649 143.324 17.7573 143.312 16.0978C143.322 14.4644 143.99 13.4568 145.008 12.7479V12.7479ZM149.189 25.9712C148.371 25.9712 146.396 26.1063 145.55 26.1714C145.158 26.2023 143.854 26.044 143.854 24.9455C143.854 23.6445 145.277 23.442 146.095 23.4757C146.614 23.4938 147.531 23.5642 148.647 23.5642C150.982 23.5642 153.21 23.1016 154.912 21.9203C156.614 20.7435 157.721 18.7882 157.71 16.0978C157.708 14.9966 157.404 14.0541 157.097 13.1345C156.88 12.4645 157.634 11.4312 158.921 10.7612C159.154 10.639 159.349 10.3352 159.138 9.97426L158.351 8.6186C158.169 8.33043 157.958 8.26814 157.675 8.34088C157.219 8.46025 155.649 9.22911 154.859 10.26C153.307 9.16923 151.222 8.54586 148.731 8.54586C146.236 8.54586 143.902 9.25483 142.329 10.5691C140.751 11.8858 139.85 13.8202 139.853 16.0978C139.85 18.7364 141.101 20.6475 142.643 21.8681C141.952 22.3592 141.045 23.2394 141.045 24.7767C141.045 25.8105 141.468 26.7296 142.256 27.4125C140.212 28.3996 138.004 29.8718 138.019 33.2221C138.019 33.7362 138.169 34.2193 138.295 34.6035C138.536 35.3491 137.923 36.3747 136.51 37.1332C136.237 37.2811 136.13 37.603 136.289 37.9249L137.103 39.3119C137.229 39.5302 137.485 39.6339 137.72 39.5639C138.698 39.2782 139.926 38.5121 140.562 37.7043C142.78 39.3067 145.831 40 148.997 40C157.057 40 160.002 36.4447 160.002 32.991C160.002 29.3758 156.796 25.9712 149.189 25.9712' fill='white'/%3E%3Cpath d='M80.1136 17.2898C80.263 15.5057 80.7975 14.1424 81.6076 13.2128C82.5117 12.1895 83.7752 11.6337 85.4542 11.6285C87.1966 11.6337 88.5436 12.2024 89.5034 13.2385C90.3746 14.1762 90.9444 15.5319 91.1012 17.2898H80.1136ZM85.6466 8.43929C82.8762 8.43407 80.5541 9.28612 78.9614 11.0807C77.3711 12.87 76.543 15.5266 76.543 19.004C76.543 22.6887 77.4926 25.3611 79.1993 27.0985C80.9014 28.8332 83.2842 29.5707 85.9836 29.5707H85.9962C88.8853 29.5707 91.2176 28.4333 93.0968 26.9944C93.3347 26.8156 93.388 26.4699 93.2159 26.2207L92.0939 24.5926C91.8991 24.3068 91.5417 24.2964 91.3163 24.4676C90.0175 25.4519 87.8119 26.3867 85.8038 26.3815C84.2032 26.3767 82.8664 25.904 81.8788 24.8884C80.9747 23.956 80.3288 22.5252 80.1262 20.4424H93.8919C94.1882 20.4424 94.4312 20.1957 94.4363 19.8894C94.4363 19.8894 94.4767 19.2789 94.4767 18.2637C94.4767 15.6432 93.7752 13.1244 92.2965 11.3274C90.8202 9.53008 88.5612 8.43407 85.6466 8.43929' fill='white'/%3E%3Cpath d='M51.0941 17.2898C51.2434 15.5057 51.7752 14.1424 52.5881 13.2128C53.4922 12.1895 54.7506 11.6337 56.4323 11.6285C58.1743 11.6337 59.524 12.2024 60.4889 13.2385C61.3523 14.1762 61.9221 15.5319 62.0793 17.2898H51.0941ZM56.6271 8.43929C53.8543 8.43407 51.5346 9.28612 49.9419 11.0807C48.3492 12.87 47.5234 15.5266 47.5234 19.004C47.5234 22.6887 48.473 25.3611 50.1774 27.0985C51.8818 28.8332 54.2646 29.5707 56.9641 29.5707H56.9739C59.8658 29.5707 62.1957 28.4333 64.0745 26.9944C64.3128 26.8156 64.3657 26.4699 64.196 26.2207L63.072 24.5926C62.8768 24.3068 62.5221 24.2964 62.2968 24.4676C60.9953 25.4519 58.7924 26.3867 56.7815 26.3815C55.1813 26.3767 53.8492 25.904 52.8593 24.8884C51.9551 23.956 51.3069 22.5252 51.1043 20.4424H64.8697C65.1687 20.4424 65.4117 20.1957 65.4191 19.8894C65.4191 19.8894 65.4571 19.2789 65.4571 18.2637C65.4571 15.6432 64.7556 13.1244 63.2742 11.3274C61.7979 9.53008 59.5444 8.43407 56.6271 8.43929' fill='white'/%3E%3C/svg%3E%0A"; prop.alt "Logo" ] ]
            ]
            Html.div [
                prop.className "flex flex-col items-center justify-center h-full"
                prop.children [
                    Html.h1 [
                        prop.className "text-center text-5xl font-bold text-white mb-3 rounded-md p-4"
                        prop.text "Elering Dashboard"
                    ]
                    ViewComponents.eleringDashboard model dispatch
                ]
            ]
        ]
    ]