## necessary for release:

- introduction
- portable lib
- nuget
- appveyor

## not necessary for release:

- reentrancy checks for watchables (both get and, for variables, set)
- thread assertions
- exception caching for computed watchables
- throwing subscribers shouldn't crash observables
- function to evaluate with dependency exception
