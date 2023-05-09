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

    time = data[:, 0]
    voltage = denoise_tv_chambolle((data[:, 1]), smooth)
    voltage = gaussian_filter(voltage, 1)
    return time, voltage


def circle(time, voltage):
    def nearest_value(items_x, items_y, value_x, value_y):
        l = []
        for i in range(len(items_x)):
            l.append(sqrt((value_x - items_x[i]) ** 2 + (value_y - items_y[i]) ** 2))
        return (l.index(min(l)))

    def flat(x, y, n):
        del x[::n]
        del y[::n]
        dat = [x, y]
        return dat

    def radius(x_c, y_c, x_1, y_1, x_2, y_2):
        cent1_x = (x_c + x_1) / 2
        cent1_y = (y_c + y_1) / 2
        cent2_x = (x_c + x_2) / 2
        cent2_y = (y_c + y_2) / 2

        k1 = (cent1_x - x_c) / (cent1_y - y_c)
        b1 = cent1_y + k1 * cent1_x

        k2 = (cent2_x - x_c) / (cent2_y - y_c)
        b2 = cent2_y + k2 * cent2_x

        x_r = (b2 - b1) / (k2 - k1)
        y_r = -k1 * x_r + b1

        rad = sqrt((x_r - x_c) ** 2 + (y_r - y_c) ** 2)
        return rad, x_r, y_r

    x = list(time)
    y = list(voltage)

    l = 32
    dff = flat(x, y, 32)
    ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))

    o = 10

    while ma + o >= len(dff[0]):
        l = l // 2
        dff = flat(x, y, l)
        ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))
    
    while dff[1][ma + o] - dff[1][ma] >= 8:
        if (dff[1][ma + o] - dff[1][ma]) / (dff[1][ma + (o - 1)] - dff[1][ma]) > 3.5:
            o -= 1
            ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))
            break
        o -= 1
        ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))

    rad, x_r, y_r = radius(dff[0][ma], dff[1][ma], dff[0][ma + o], dff[1][ma + o], dff[0][ma - o], dff[1][ma - o])
    return rad, x_r, y_r