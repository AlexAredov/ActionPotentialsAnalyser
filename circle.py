#importing libs
import matplotlib.pyplot as plt
import pandas as pd
from math import sqrt
from scipy.ndimage import gaussian_filter
from io import StringIO
from skimage.restoration import denoise_tv_chambolle
import numpy as np

def preprocess(file, smooth = 15):
    with open("./" + file, 'r') as file:
        file_content = file.read()
    file_content = file_content.replace(',', '.')

    with StringIO(file_content) as file_buffer:
        data = np.genfromtxt(file_buffer, delimiter='\t')

    time = data[:, 0] * 1000
    voltage = denoise_tv_chambolle((data[:, 1] * 1000), smooth)
    voltage = gaussian_filter(voltage, 1)
    return time, voltage

def circle(time, voltage):
    #grid
    plt.style.use('seaborn-whitegrid')

    #the function to found the nearest value for this
    def nearest_value(items_x, items_y, value_x, value_y):
        l = []
        for i in range(len(items_x)):
            l.append(sqrt((value_x - items_x[i])**2 + (value_y - items_y[i])**2))
        return(l.index(min(l)))

    #function to do graph smoother
    def flat(x, y, n):
        x1 = []
        y1 = []
        for i in range(0,len(x),n):
            x1.append(x[i])
            y1.append(y[i])
        dat = [x1, y1]
        return dat

    #function to find center dot and radius of the circle from 3 dots
    def radius(x_c, y_c, x_1, y_1, x_2, y_2):
        cent1_x = (x_c + x_1)/2
        cent1_y = (y_c + y_1)/2
        cent2_x = (x_c + x_2)/2
        cent2_y = (y_c + y_2)/2

        k1 = (cent1_x - x_c)/(cent1_y - y_c)
        b1 = cent1_y + k1*cent1_x

        k2 = (cent2_x - x_c)/(cent2_y - y_c)
        b2 = cent2_y + k2*cent2_x

        x_r = (b2-b1)/(k2-k1)
        y_r = -k1*x_r + b1
        #print(x_r, y_r)

        rad = sqrt((x_r - x_c)**2 + (y_r - y_c)**2)
        #print(rad)

        plt.gca().add_patch(plt.Circle((x_r, y_r), rad, color='lightblue', alpha=0.5))
        return rad, x_r, y_r

    x_t = list(time)
    y_t = list(voltage)

    #writing just the right values
    x = []
    y = []
    for i in range(len(x_t)):
        if (x_t[i] < x_t[list(y_t).index(max(y_t))]) and (y_t[i] < min(y_t)/2):
            x.append(x_t[i])
            y.append(y_t[i])
    dat = {'x': x_t, 'y': y_t}
    datafr = pd.DataFrame(data=dat)

    #first flatting (the big one)
    l = 32
    dff = flat(x, y, 32)
    ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))

    #number of dots to the left and right of the center
    o = 10

    #reduce flatting
    while ma + o >= len(dff[0]):
        l = l//2
        dff = flat(x, y, l)
        ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))
    while dff[1][ma+o] - dff[1][ma] >= 3:
        o -= 1
        ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))

    #set the size of graph
    f = plt.figure()
    f.set_figwidth(5*2*max(dff[0])/abs(min(dff[1])))
    f.set_figheight(5)

    #the number of flatting
    #print(l)

    #the dot (nearest to center)
    #plt.scatter(x[list(y).index(max(y))], min(y), color='orange', s=40, marker='o')

    #other dots
    #plt.scatter(dff[0][ma], dff[1][ma], color='green', s=40, marker='o')
    #plt.scatter(dff[0][ma+o], dff[1][ma+o], color='green', s=40, marker='o')
    #plt.scatter(dff[0][ma-o], dff[1][ma-o], color='green', s=40, marker='o')

    #finding the radius
    rad, x_r, y_r = radius(dff[0][ma], dff[1][ma], dff[0][ma+o], dff[1][ma+o], dff[0][ma-o], dff[1][ma-o])

    #drauing the rigth and flat graphes
    #plt.plot(datafr.x, datafr.y)

    #plt.show()
    return rad, x_r, y_r