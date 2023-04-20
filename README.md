# UnionFlight Xplane Data Reader


The objective is to get data from xplane via a UDP connection with the simulator in C#

I thank @MaxFerretti. Thanks to the [XPlaneConnector](https://github.com/MaxFerretti/XPlaneConnector) repo  I was able to understand more quickly how udp works with Xplane.

## MVP

### From Xplane :

- [x] Can get Aircraft name
- [x] Can get Aircraft registration
- [x] Can get Aircraft Indicated Speed
- [x] Can get Aircraft groundSpeed
- [x] Can get Aircraft trueAirSpeed
- [x] Can get Aircraft heading
- [x] Can get Aircraft verticalSpeed
- [x] Can get Aircraft Position
- [x] Know if sim is launched
- [x] Can close connection


### Folder structure :
- [x] 1 folder for lib source code + 1 folder for exemple

### CI/CD :

- [ ] Can deploy package on nuget via Github Actions