#include <iostream>

float circle(float time[], float voltage[])
{
    return time[0];
}

int main()
{
    std::cout << "Hello World" << std::endl;
    float time[4] = {1.2, 2.3, 3.4, 4.5};
    float voltage[4] = {1.2, 2.3, 3.4, 4.5};
    std::cout << circle(time, voltage) << std::endl;
}

