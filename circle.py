"""
Библиотека для анализа потенциалов действий кардиомиоцитов

Как это работает?
На входе мы получаем файл с точками (в зависимости: время по оси X и значение по
оси Y). Для начала мы берем нужную нам часть графика. Мы удаляем все значения
по оси X до максимального значения Y. И удаляем ненужную верхнюю половину
значений Y.
Далее нужно сгладить график, для этого была написана функция flat.
Функция принимает 3 значение: массив значений X, массив значений Y и значение n.
Значение n - это степень сглаживания. Алгоритм оставляет все элементы в массивах
через n значений.
Берем сначала очень большую степень сглаживания (например 32), дальше мы
будем постепенно ее уменьшать.
Теперь нам нужно найти точку в которой случается перегиб. Для этого возьмем точку
у которой значение X равно максимальным из сглаженного графика и минимальное
значение Y. Далее мы можем найти ближайшее значение от точки к графику. Для
этого написана функция nearest_value.
Функция проходит по всем значениям функции и находит минимальное расстояние
от точки которую мы нашли до графика.
Чтобы найти радиус мы должны построить окружность. Окружность мы можем
построить только по трем точкам. Одна точка у нас уже есть. Теперь нам нужно
найти еще 2. Мы можем взять точку на 10 впереди этой точки и на 10 значений
назад. Но если мы видим что мы выходим пределы массива взяв на 10 точек
больше, тогда мы уменьшаем сглаживание в 2 раза. Чтобы избежать того, что круг
будет выходить за пределы графика проверяем, чтобы следующая точка была не
выше предыдущей на 0.003. Если это так, то уменьшаем разницу и берем не на 10
значений вперед, а на 10 - 1 значений. И так пока не найдем оптимальное
сглаживание и оптимальное количество точек.
Итак, мы получили 3 точки, теперь по ним мы можем найти окружность.
"""
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
    plt.style.use('seaborn-whitegrid')

    def nearest_value(items_x, items_y, value_x, value_y):
        l = []
        for i in range(len(items_x)):
            l.append(sqrt((value_x - items_x[i])**2 + (value_y - items_y[i])**2))
        return(l.index(min(l)))

    def flat(x, y, n):
        x1 = []
        y1 = []
        for i in range(0,len(x),n):
            x1.append(x[i])
            y1.append(y[i])
        dat = [x1, y1]
        return dat

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

        rad = sqrt((x_r - x_c)**2 + (y_r - y_c)**2)

        plt.gca().add_patch(plt.Circle((x_r, y_r), rad, color='lightblue', alpha=0.5))
        return rad, x_r, y_r

    x_t = list(time)
    y_t = list(voltage)

    x = []
    y = []
    for i in range(len(x_t)):
        if (x_t[i] < x_t[list(y_t).index(max(y_t))]) and (y_t[i] < min(y_t)/2):
            x.append(x_t[i])
            y.append(y_t[i])

    l = 32
    dff = flat(x, y, 32)
    ma = nearest_value(dff[0], dff[1], x[list(y).index(max(y))], min(y))

    o = 10

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
    rad, x_r, y_r = radius(dff[0][ma], dff[1][ma], dff[0][ma+o], dff[1][ma+o], dff[0][ma-o], dff[1][ma-o])
    return rad, x_r, y_r